using FamLedger.Domain.Enums;

namespace FamLedger.Common;

public static class DefaultCategories
{
    public static readonly (string Name, CategoryKind Kind, int Order)[] Items =
    [
        ("Продукты", CategoryKind.Expense, 1),
        ("Транспорт", CategoryKind.Expense, 2),
        ("Кафе", CategoryKind.Expense, 3),
        ("Здоровье", CategoryKind.Expense, 4),
        ("Развлечения", CategoryKind.Expense, 5),
        ("Одежда", CategoryKind.Expense, 6),
        ("Коммуналка", CategoryKind.Expense, 7),
        ("Подписки", CategoryKind.Expense, 8),
        ("Прочее", CategoryKind.Expense, 99),
        ("Зарплата", CategoryKind.Income, 1),
        ("Другое", CategoryKind.Income, 99)
    ];
}
