using Microsoft.Extensions.DependencyInjection;
using Pos.Application;
using Pos.Domain;
using Pos.Infrastructure;

var services = new ServiceCollection()
    .AddSingleton<IStudentRepository, InMemoryStudentRepository>()
    .AddSingleton<ICriterionRepository, InMemoryCriterionRepository>()
    .AddSingleton<IEvaluationRepository, InMemoryEvaluationRepository>()
    .AddSingleton<EvaluationFactory>()
    .AddSingleton<IFinalScoreStrategy, WeightedFinalScoreStrategy>()
    .AddSingleton<FinalScoreDomainService>()
    .AddSingleton<IResultExporter, CsvResultExporter>()
    .AddSingleton<PosApplicationFacade>()
    .BuildServiceProvider();

var app = services.GetRequiredService<PosApplicationFacade>();
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("ПОС — преподавательская оценка студентов");

while (true)
{
    Console.WriteLine("\n1. Добавить студента  2. Список студентов  3. Добавить критерий  4. Список критериев");
    Console.WriteLine("5. Выставить оценку   6. Список оценок    7. Итоги              8. Экспорт итогов");
    Console.WriteLine("0. Выход");
    Console.Write("Выберите действие: ");
    switch (Console.ReadLine())
    {
        case "1":
            Try(() => Console.WriteLine($"Добавлен студент: {app.AddStudent(ReadRequired("Имя: ")).Name}"));
            break;
        case "2":
            foreach (var student in app.GetStudents()) Console.WriteLine($"{student.Id}: {student.Name}");
            break;
        case "3":
            Try(() => Console.WriteLine($"Добавлен критерий: {app.AddCriterion(ReadRequired("Название: "), ReadDecimal("Максимальный балл: "), ReadDecimal("Вес: ")).Name}"));
            break;
        case "4":
            foreach (var criterion in app.GetCriteria()) Console.WriteLine($"{criterion.Id}: {criterion.Name}, максимум {criterion.MaximumScore}, вес {criterion.Weight}");
            break;
        case "5":
            Try(() =>
            {
                var student = SelectStudent(app);
                var criterion = SelectCriterion(app);
                app.SetEvaluation(student.Id, criterion.Id, ReadDecimal($"Балл (0..{criterion.MaximumScore}): "), ReadOptional("Комментарий: "));
                Console.WriteLine("Оценка сохранена.");
            });
            break;
        case "6":
            foreach (var evaluation in app.GetEvaluations())
                Console.WriteLine($"{evaluation.StudentName}: {evaluation.CriterionName} = {evaluation.Score}/{evaluation.MaximumScore}" +
                                  (evaluation.Comment is null ? "" : $" ({evaluation.Comment})"));
            break;
        case "7":
            foreach (var result in app.CalculateResults()) Console.WriteLine($"{result.StudentName}: {result.TotalScore:0.##}/{result.MaximumScore:0.##}");
            break;
        case "8":
            Try(() =>
            {
                var path = ReadOptional("Путь (по умолчанию results.csv): ") ?? "results.csv";
                app.ExportResults(path);
                Console.WriteLine($"Итоги экспортированы в {path}");
            });
            break;
        case "0":
            return;
        default:
            Console.WriteLine("Неизвестная команда.");
            break;
    }
}

static string ReadRequired(string prompt)
{
    Console.Write(prompt);
    var value = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Значение не может быть пустым.");
    return value;
}

static string? ReadOptional(string prompt)
{
    Console.Write(prompt);
    var value = Console.ReadLine();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static decimal ReadDecimal(string prompt)
{
    Console.Write(prompt);
    if (!decimal.TryParse(Console.ReadLine(), out var value)) throw new ArgumentException("Введите число.");
    return value;
}

static Student SelectStudent(PosApplicationFacade app)
{
    var items = app.GetStudents().ToArray();
    if (items.Length == 0) throw new InvalidOperationException("Сначала добавьте студента.");
    for (var i = 0; i < items.Length; i++) Console.WriteLine($"{i + 1}. {items[i].Name}");
    Console.Write("Номер студента: ");
    return items[ReadIndex(items.Length)];
}

static Criterion SelectCriterion(PosApplicationFacade app)
{
    var items = app.GetCriteria().ToArray();
    if (items.Length == 0) throw new InvalidOperationException("Сначала добавьте критерий.");
    for (var i = 0; i < items.Length; i++) Console.WriteLine($"{i + 1}. {items[i].Name}");
    Console.Write("Номер критерия: ");
    return items[ReadIndex(items.Length)];
}

static int ReadIndex(int length)
{
    if (!int.TryParse(Console.ReadLine(), out var number) || number < 1 || number > length)
        throw new ArgumentException("Некорректный номер.");
    return number - 1;
}

static void Try(Action action)
{
    try { action(); }
    catch (Exception exception) { Console.WriteLine($"Ошибка: {exception.Message}"); }
}
