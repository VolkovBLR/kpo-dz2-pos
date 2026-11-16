namespace Pos.Domain;

public readonly record struct StudentId(Guid Value)
{
    public static StudentId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct CriterionId(Guid Value)
{
    public static CriterionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct EvaluationId(Guid Value)
{
    public static EvaluationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public sealed class Score
{
    public decimal Value { get; }
    public decimal Maximum { get; }

    private Score(decimal value, decimal maximum)
    {
        Value = value;
        Maximum = maximum;
    }

    public static Score Create(decimal value, decimal maximum)
    {
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum), "Максимум должен быть положительным.");
        if (value < 0 || value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"Балл должен быть в диапазоне от 0 до {maximum}.");
        return new Score(value, maximum);
    }

    public decimal Ratio => Value / Maximum;
}

public sealed class Student
{
    public StudentId Id { get; }
    public string Name { get; private set; }

    public Student(StudentId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Имя студента не может быть пустым.", nameof(name));
        Id = id;
        Name = name.Trim();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Имя студента не может быть пустым.", nameof(name));
        Name = name.Trim();
    }
}

public sealed class Criterion
{
    public CriterionId Id { get; }
    public string Name { get; private set; }
    public decimal MaximumScore { get; private set; }
    public decimal Weight { get; private set; }

    public Criterion(CriterionId id, string name, decimal maximumScore, decimal weight)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Название критерия не может быть пустым.", nameof(name));
        if (maximumScore <= 0) throw new ArgumentOutOfRangeException(nameof(maximumScore), "Максимум должен быть положительным.");
        if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight), "Вес должен быть положительным.");
        Id = id;
        Name = name.Trim();
        MaximumScore = maximumScore;
        Weight = weight;
    }
}

public sealed class Evaluation
{
    public EvaluationId Id { get; }
    public StudentId StudentId { get; }
    public CriterionId CriterionId { get; }
    public Score Score { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public string? Comment { get; private set; }

    internal Evaluation(EvaluationId id, StudentId studentId, CriterionId criterionId, Score score, string? comment)
    {
        Id = id;
        StudentId = studentId;
        CriterionId = criterionId;
        Score = score;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeScore(Score score, string? comment = null)
    {
        Score = score;
        Comment = string.IsNullOrWhiteSpace(comment) ? Comment : comment.Trim();
    }
}

public sealed class EvaluationFactory
{
    public Evaluation Create(Student student, Criterion criterion, decimal score, string? comment = null)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(criterion);
        return new Evaluation(EvaluationId.New(), student.Id, criterion.Id, Score.Create(score, criterion.MaximumScore), comment);
    }
}

public interface IStudentRepository
{
    IReadOnlyCollection<Student> GetAll();
    Student? Get(StudentId id);
    void Add(Student student);
}

public interface ICriterionRepository
{
    IReadOnlyCollection<Criterion> GetAll();
    Criterion? Get(CriterionId id);
    void Add(Criterion criterion);
}

public interface IEvaluationRepository
{
    IReadOnlyCollection<Evaluation> GetAll();
    Evaluation? GetFor(StudentId studentId, CriterionId criterionId);
    void Add(Evaluation evaluation);
    void Remove(StudentId studentId, CriterionId criterionId);
}

public sealed record StudentResult(Student Student, decimal TotalScore, decimal MaximumScore);

public interface IFinalScoreStrategy
{
    StudentResult Calculate(Student student, IEnumerable<Criterion> criteria, IEnumerable<Evaluation> evaluations);
}

public sealed class WeightedFinalScoreStrategy : IFinalScoreStrategy
{
    public StudentResult Calculate(Student student, IEnumerable<Criterion> criteria, IEnumerable<Evaluation> evaluations)
    {
        var criterionList = criteria.ToList();
        var evaluationByCriterion = evaluations.ToDictionary(x => x.CriterionId);
        var totalWeight = criterionList.Sum(x => x.Weight);
        if (totalWeight == 0) return new StudentResult(student, 0, 0);

        var total = criterionList.Sum(criterion =>
            evaluationByCriterion.TryGetValue(criterion.Id, out var evaluation)
                ? evaluation.Score.Ratio * criterion.Weight
                : 0);
        return new StudentResult(student, total / totalWeight * 10, 10);
    }
}

public sealed class FinalScoreDomainService
{
    private readonly IFinalScoreStrategy strategy;
    public FinalScoreDomainService(IFinalScoreStrategy strategy) => this.strategy = strategy;

    public StudentResult Calculate(Student student, IEnumerable<Criterion> criteria, IEnumerable<Evaluation> evaluations) =>
        strategy.Calculate(student, criteria, evaluations);
}
