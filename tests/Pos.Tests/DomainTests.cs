using Pos.Application;
using Pos.Domain;
using Pos.Infrastructure;

namespace Pos.Tests;

public sealed class DomainTests
{
    [Fact]
    public void Score_cannot_exceed_criterion_maximum()
    {
        var criterion = new Criterion(CriterionId.New(), "Практика", 10, 1);
        var student = new Student(StudentId.New(), "Иван");
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvaluationFactory().Create(student, criterion, 11));
    }

    [Fact]
    public void Weighted_strategy_calculates_result_from_current_evaluations()
    {
        var student = new Student(StudentId.New(), "Иван");
        var first = new Criterion(CriterionId.New(), "Код", 10, 2);
        var second = new Criterion(CriterionId.New(), "Защита", 5, 1);
        var factory = new EvaluationFactory();
        var result = new WeightedFinalScoreStrategy().Calculate(student, new[] { first, second }, new[]
        {
            factory.Create(student, first, 8),
            factory.Create(student, second, 5)
        });

        Assert.Equal(8.67m, decimal.Round(result.TotalScore, 2));
    }

    [Fact]
    public void Facade_rejects_evaluation_for_unknown_criterion()
    {
        var facade = new PosApplicationFacade(
            new InMemoryStudentRepository(),
            new InMemoryCriterionRepository(),
            new InMemoryEvaluationRepository(),
            new EvaluationFactory(),
            new FinalScoreDomainService(new WeightedFinalScoreStrategy()),
            new CsvResultExporter());
        var student = facade.AddStudent("Иван");

        Assert.Throws<InvalidOperationException>(() => facade.SetEvaluation(student.Id, CriterionId.New(), 1));
    }
}
