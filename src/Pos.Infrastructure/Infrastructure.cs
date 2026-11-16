using System.Text;
using System.Text.Json;
using Pos.Application;
using Pos.Domain;

namespace Pos.Infrastructure;

public sealed class InMemoryStudentRepository : IStudentRepository
{
    private readonly Dictionary<StudentId, Student> items = new();
    public IReadOnlyCollection<Student> GetAll() => items.Values.ToArray();
    public Student? Get(StudentId id) => items.GetValueOrDefault(id);
    public void Add(Student student) => items.Add(student.Id, student);
}

public sealed class InMemoryCriterionRepository : ICriterionRepository
{
    private readonly Dictionary<CriterionId, Criterion> items = new();
    public IReadOnlyCollection<Criterion> GetAll() => items.Values.ToArray();
    public Criterion? Get(CriterionId id) => items.GetValueOrDefault(id);
    public void Add(Criterion criterion) => items.Add(criterion.Id, criterion);
}

public sealed class InMemoryEvaluationRepository : IEvaluationRepository
{
    private readonly Dictionary<(StudentId, CriterionId), Evaluation> items = new();
    public IReadOnlyCollection<Evaluation> GetAll() => items.Values.ToArray();
    public Evaluation? GetFor(StudentId studentId, CriterionId criterionId) => items.GetValueOrDefault((studentId, criterionId));
    public void Add(Evaluation evaluation) => items[(evaluation.StudentId, evaluation.CriterionId)] = evaluation;
    public void Remove(StudentId studentId, CriterionId criterionId) => items.Remove((studentId, criterionId));
}

public sealed class CsvResultExporter : IResultExporter
{
    public void Export(string path, IReadOnlyCollection<ResultView> results)
    {
        var lines = new List<string> { "StudentName,TotalScore,MaximumScore" };
        lines.AddRange(results.Select(result =>
            $"{Csv.Escape(result.StudentName)},{result.TotalScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{result.MaximumScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"));
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }
}

public sealed class JsonDataStore : IDataStore
{
    private sealed record Snapshot(List<StudentRecord> Students, List<CriterionRecord> Criteria, List<EvaluationRecord> Evaluations);
    private sealed record StudentRecord(Guid Id, string Name);
    private sealed record CriterionRecord(Guid Id, string Name, decimal MaximumScore, decimal Weight);
    private sealed record EvaluationRecord(Guid StudentId, Guid CriterionId, decimal Score, string? Comment);

    public void Save(string path, IReadOnlyCollection<Student> students, IReadOnlyCollection<Criterion> criteria, IReadOnlyCollection<Evaluation> evaluations)
    {
        var snapshot = new Snapshot(
            students.Select(x => new StudentRecord(x.Id.Value, x.Name)).ToList(),
            criteria.Select(x => new CriterionRecord(x.Id.Value, x.Name, x.MaximumScore, x.Weight)).ToList(),
            evaluations.Select(x => new EvaluationRecord(x.StudentId.Value, x.CriterionId.Value, x.Score.Value, x.Comment)).ToList());
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }

    public (IReadOnlyCollection<Student> Students, IReadOnlyCollection<Criterion> Criteria, IReadOnlyCollection<Evaluation> Evaluations) Load(string path)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path)) ?? throw new InvalidDataException("Пустой файл данных.");
        var students = snapshot.Students.Select(x => new Student(new StudentId(x.Id), x.Name)).ToArray();
        var criteria = snapshot.Criteria.Select(x => new Criterion(new CriterionId(x.Id), x.Name, x.MaximumScore, x.Weight)).ToArray();
        var criterionMap = criteria.ToDictionary(x => x.Id);
        var evaluations = snapshot.Evaluations.Select(x =>
        {
            var student = students.Single(s => s.Id.Value == x.StudentId);
            var criterion = criterionMap[new CriterionId(x.CriterionId)];
            return new EvaluationFactory().Create(student, criterion, x.Score, x.Comment);
        }).ToArray();
        return (students, criteria, evaluations);
    }
}
