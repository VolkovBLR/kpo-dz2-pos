using System.Globalization;
using System.Text;
using Pos.Domain;

namespace Pos.Application;

public sealed record EvaluationView(string StudentName, string CriterionName, decimal Score, decimal MaximumScore, string? Comment);
public sealed record ResultView(string StudentName, decimal TotalScore, decimal MaximumScore);

public interface IResultExporter
{
    void Export(string path, IReadOnlyCollection<ResultView> results);
}

public interface IDataStore
{
    void Save(string path, IReadOnlyCollection<Student> students, IReadOnlyCollection<Criterion> criteria, IReadOnlyCollection<Evaluation> evaluations);
    (IReadOnlyCollection<Student> Students, IReadOnlyCollection<Criterion> Criteria, IReadOnlyCollection<Evaluation> Evaluations) Load(string path);
}

public sealed class PosApplicationFacade
{
    private readonly IStudentRepository students;
    private readonly ICriterionRepository criteria;
    private readonly IEvaluationRepository evaluations;
    private readonly EvaluationFactory evaluationFactory;
    private readonly FinalScoreDomainService finalScoreService;
    private readonly IResultExporter exporter;

    public PosApplicationFacade(
        IStudentRepository students,
        ICriterionRepository criteria,
        IEvaluationRepository evaluations,
        EvaluationFactory evaluationFactory,
        FinalScoreDomainService finalScoreService,
        IResultExporter exporter)
    {
        this.students = students;
        this.criteria = criteria;
        this.evaluations = evaluations;
        this.evaluationFactory = evaluationFactory;
        this.finalScoreService = finalScoreService;
        this.exporter = exporter;
    }

    public Student AddStudent(string name)
    {
        var student = new Student(StudentId.New(), name);
        students.Add(student);
        return student;
    }

    public Criterion AddCriterion(string name, decimal maximumScore, decimal weight)
    {
        var criterion = new Criterion(CriterionId.New(), name, maximumScore, weight);
        criteria.Add(criterion);
        return criterion;
    }

    public Evaluation SetEvaluation(StudentId studentId, CriterionId criterionId, decimal score, string? comment = null)
    {
        var student = students.Get(studentId) ?? throw new InvalidOperationException("Студент не найден.");
        var criterion = criteria.Get(criterionId) ?? throw new InvalidOperationException("Критерий не найден в рубрикаторе.");
        var evaluation = evaluationFactory.Create(student, criterion, score, comment);
        evaluations.Remove(studentId, criterionId);
        evaluations.Add(evaluation);
        return evaluation;
    }

    public IReadOnlyCollection<Student> GetStudents() => students.GetAll();
    public IReadOnlyCollection<Criterion> GetCriteria() => criteria.GetAll();

    public IReadOnlyCollection<EvaluationView> GetEvaluations() =>
        evaluations.GetAll().Select(e =>
        {
            var student = students.Get(e.StudentId)!;
            var criterion = criteria.Get(e.CriterionId)!;
            return new EvaluationView(student.Name, criterion.Name, e.Score.Value, e.Score.Maximum, e.Comment);
        }).ToArray();

    public IReadOnlyCollection<ResultView> CalculateResults() =>
        students.GetAll().Select(student =>
        {
            var result = finalScoreService.Calculate(student, criteria.GetAll(), evaluations.GetAll().Where(e => e.StudentId == student.Id));
            return new ResultView(result.Student.Name, result.TotalScore, result.MaximumScore);
        }).ToArray();

    public void ExportResults(string path) => exporter.Export(path, CalculateResults());
}

public static class Csv
{
    public static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
