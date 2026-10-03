# LogiCore

Программа моделирует работу транспортно-логистической компании: грузы, транспорт, заказы, подбор самой дешёвой машины, события, отчёты и сохранение состояния.


## Структура

```text
OOP_LogiCore/
├── LogiCore.Domain/   # Модель и бизнес-логика, не использует Console
├── LogiCore.App/      # Демо, меню, два подписчика на события
├── LogiCore.Tests/    # xUnit-тесты
├── Docs/              # UML и подробности
└── LogiCore.sln
```

## Объём работы

Приоритетные блоки дают 60 баллов: доменная модель - 15, T3 - 10, T4 - 10,
T5 - 10, T6 - 5, T10 - 5, архитектура и оформление - 5. При этом в проекте
сохранены все обязательные T1-T10, демонстрационный сценарий и тесты. Итоговую
оценку определяет преподаватель после устной защиты.




## Быстрый запуск
Откройте `LogiCore.sln` в Rider и запустите проект `LogiCore.App`.

Из терминала:
```bash
dotnet restore --configfile NuGet.Config
dotnet build --no-restore
dotnet run --project LogiCore.App --no-build -- --demo-only
dotnet test --no-restore
```
При запуске приложение само показывает обязательный демо-сценарий. Без параметра `--demo-only` после него открывается простое меню.

## Соответствие требованиям

| Требование | Где реализовано |
| --- | --- |
| 3.1, T1, T2 - транспорт, инкапсуляция, полиморфизм | `Vehicle.cs`: абстрактный `Vehicle`, 5 наследников, `virtual`, `override`, `sealed`, `base.CanCarry`, приватный setter состояния |
| 3.2 - грузы и совместимость | `Cargo.cs`, `Services.cs`: 5 грузов и отдельный `CargoCompatibilityValidator` |
| 3.3 - клиент, маршрут, заказ | `Order.cs`, `Route.cs`: read-only история, `RoutePoint`, оператор `-`, машина состояний |
| 3.4 - диспетчер | `Services.cs`: выбор самого дешёвого свободного и ещё не назначенного транспорта без исключений в обычном переборе |
| T3 - интерфейсы и вариантность | `Common.cs`, `Demo.cs`: 5 интерфейсов, явная реализация `IInsurable`, `out T`, `in T` и демонстрация присваивания |
| T4 - обобщения | `Repository.cs`: `Repository<T>`, индексатор, `Predicate<T>`, `yield return`, метод `ToReportTable<T>` |
| T5 - события | `Services.cs`, `Notifications.cs`: 4 события, собственный delegate, консоль и файл, отписка |
| T6 - исключения | `Exceptions.cs`, `Persistence.cs`, `Demo.cs`: иерархия исключений, `when`, `throw;`, `using`, `finally` |
| T7 - 5 паттернов | Strategy, Decorator, Factory Method, Observer и Singleton в `Patterns.cs` и `Services.cs` |
| T8 - LINQ | `Reports.cs`: 6 отчётов, настоящий `join`, query syntax, `Where`, `Select`, `OrderByDescending`, `GroupBy`, `Sum`, `Average`, `Count`, `ToDictionary`, `ToLookup` |
| T9 - JSON | `Persistence.cs`: полный DTO-снимок транспорта, клиентов, грузов, маршрутов, заказов и связей; восстановление точных типов и Id |
| T10 - enum и struct | `Common.cs`, `Route.cs`: `[Flags]`, побитовая операция, структура, оператор `-`, explicit string |
| Тесты | `DomainTests.cs`: 27 xUnit-тестов стоимости, правил совместимости, состояний, репозитория, декораторов и JSON round-trip |
| UML | `Docs/LogiCore.puml` |

## Пять паттернов

1. **Strategy** - тариф передаётся в сервис через `ITariffStrategy`; можно выбрать обычный или срочный.
2. **Decorator** - страховка, упаковка и срочность входят в реальную стоимость заказа; порядок слоёв влияет на результат.
3. **Factory Method** - наследники `VehicleFactory` переопределяют `Create` и создают разные типы транспорта.
4. **Observer** - `ConsoleNotifier` и `FileLogger` подписаны на события `DeliveryService`.
5. **Singleton** - `LogisticsSettings.Instance` хранит используемый порог отчёта через потокобезопасный `Lazy<T>`.

## Что показывает автоматическое демо

- 6 транспортных средств и 10 грузов пяти типов;
- 5 завершённых заказов, несовместимые грузы и попытку перегруза;
- реальную цепочку «транспорт -> тариф -> 3 декоратора -> цена заказа -> выручка»;
- четыре события с выводом в консоль и файл;
- шесть LINQ-отчётов;
- сохранение полного состояния, восстановление типов и связей и повторный отчёт;
- настоящие примеры контравариантности `in` и ковариантности `out`.

[![Проверка проекта](https://github.com/SemYeat/OOP_LogiCore_Kharkovskii/actions/workflows/dotnet.yml/badge.svg)](https://github.com/SemYeat/OOP_LogiCore_Kharkovskii/actions/workflows/dotnet.yml)


