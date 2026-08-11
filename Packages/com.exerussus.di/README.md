# Exerussus.DI

DI-контейнер для Unity: инъекция по атрибутам на полях и свойствах, скоупы, потокобезопасный
контейнер с ожиданием регистраций.

Это инжектор полей и типизированный локатор, **не** IoC с автоконструированием. Держите в нём
системы и сервисы, а не горячие данные.

## Установка

Пакет лежит в подпапке репозитория, поэтому в git-ссылке нужен параметр `?path=`.

Unity Package Manager → **+** → *Add package from git URL*:

```
https://github.com/exerussus/DI.git?path=/Packages/com.exerussus.di
```

Чтобы закрепиться на версии, добавьте тег после `#` — фрагмент всегда идёт **после** `?path=`:

```
https://github.com/exerussus/DI.git?path=/Packages/com.exerussus.di#v2.0.0
```

Либо строкой в `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.exerussus.di": "https://github.com/exerussus/DI.git?path=/Packages/com.exerussus.di#v2.0.0"
  }
}
```

Требуется Unity 2021.3+ и установленный git в `PATH` — UPM дёргает системный клиент, и без него
git-ссылка молча не резолвится.

## Сборки

| Сборка | Зависимости | Назначение |
|---|---|---|
| `Exerussus.DI` | нет, включая движок | Ядро: контейнер, инъекция, скоупы |
| `Exerussus.DI.Unity` | ядро, UnityEngine | Логгер в консоль, фабрика контейнеров |
| `Exerussus.DI.Async` | ядро, UniTask | Конкурентный контейнер и async-инъекция |

Ядро собирается с `noEngineReferences: true` — оно не тянет `UnityEngine` и работает в любом
контексте, включая чистые C#-сборки.

Async-сборка включается только при установленном UniTask: `versionDefines` выставляет
`EXERUSSUS_DI_UNITASK`, а `defineConstraints` без него исключает сборку из компиляции.

## Регистрация

```csharp
var container = UnityContainers.Create();          // с логгером Unity
var container = new DependenciesContainer();       // без логирования

container.Add<IPlayerService>(new PlayerService());  // по интерфейсу, проверяется компилятором
container.Add(typeof(IInputService), input);         // по типу в рантайме, проверяется на регистрации
container.AddByRuntimeType(new SaveService());       // по фактическому типу инстанса
```

`Add(Type, object)` бросает `ArgumentException`, если инстанс не приводится к ключу. Благодаря этому
`Get<T>` и `TryGet<T>` не могут упасть на касте.

### Повторная регистрация

```csharp
var strict = new DependenciesContainer(null, null, DuplicateRegistrationPolicy.Throw);
container.Add<IPlayerService>(service, DuplicateRegistrationPolicy.Ignore);
```

`Warn` (по умолчанию) перезаписывает и пишет в лог, `Overwrite` — молча, `Throw` — бросает,
`Ignore` — оставляет первую регистрацию.

## Инъекция

```csharp
public class Game
{
    [Inject] private IPlayerService _player;
    [Inject] public IInputService Input { get; set; }
}

container.Inject(game);                            // бросает при нехватке
container.TryInject(game, out var missing);        // сообщает, чего не хватило
```

`Inject` и `TryInject` транзакционны: сначала проверяются все точки, запись идёт только если
разрешились все. Половинчато заполненного объекта не бывает. `TryInject` отдаёт первую
неразрешённую точку в виде `MissingDependency`.

Поддерживаются приватные поля и свойства, включая унаследованные, в том числе `readonly`-поля:
контейнер пишет их через рефлексию, а компилятор защищает поле от перезаписи из вашего кода —
это рекомендуемый стиль объявления зависимости. **Не** поддерживаются: статические члены,
свойства без сеттера, индексаторы — по каждому случаю бросается исключение с указанием члена,
а не тихо пропускается.

### Отсутствующие зависимости

```csharp
container.Inject(game, type =>
{
    if (type == typeof(IOptionalService)) return MissingDependencyResult.Skip();
    if (type == typeof(IScratchService))  return MissingDependencyResult.Use(new ScratchService());
    return MissingDependencyResult.Register(Activator.CreateInstance(type));
});
```

`Use` подставляет инстанс только в этот объект, `Register` ещё и кладёт его в контейнер.
В 1.x это было одним поведением.

## Provide

```csharp
public class Bootstrap
{
    [Provide] private readonly IPlayerService _player = new PlayerService();
    [Provide] private readonly IInputService _input = new InputService();
}

container.ValidateProvide(bootstrap);   // бросит, если тип уже зарегистрирован
container.Provide(bootstrap);
```

`ValidateProvide` смотрит только собственные регистрации контейнера: перекрытие родительского типа
в дочернем скоупе — легальный сценарий, а не конфликт. Не бросающий вариант — `TryValidateProvide`,
он отдаёт `ProvideConflict`.

## Скоупы

```csharp
var scope = container.CreateScope();
scope.Add<ILevelService>(levelService);

scope.Get<IPlayerService>();    // из родителя
scope.Has<IPlayerService>();    // true  — с учётом цепочки
scope.HasOwn<IPlayerService>(); // false — только свои
```

Регистрация всегда локальна и в родителя не поднимается. Родитель задаётся только в конструкторе,
поэтому цикл в цепочке построить нельзя.

## Асинхронный контейнер

Снимает зависимость от порядка бутстрапа: система стартует, не дожидаясь, пока кто-то другой
зарегистрирует её зависимость.

```csharp
var container = new ConcurrentDependenciesContainer();

// Поток A
var save = await container.GetAsync<ISaveService>(cancellationToken);

// Поток B, позже
container.Add<ISaveService>(new SaveService());   // будит всех ждущих
```

```csharp
await AsyncInjector.InjectAsync(container, game, cancellationToken);
```

`InjectAsync` заполняет найденное сразу и ждёт только недостающее. В отличие от синхронного `Inject`
она **не транзакционна**: при отмене часть членов уже заполнена.

### Потоки

По умолчанию `resumeOnMainThread: true` — продолжение после ожидания возвращается на главный поток
через `UniTask.SwitchToMainThread()`. Без этого код продолжился бы на потоке, вызвавшем `Add`, что для
Unity-объектов недопустимо. Отключайте только если точно работаете с чистыми C#-объектами.

Политика дублей под параллельной записью — best-effort: проверка и запись не атомарны. Регистрация
рассчитана на фазу бутстрапа.

### Сторож зависаний

Если ожидание длится дольше `waitWarningSeconds` (по умолчанию 5), в лог уходит предупреждение со
списком типов, которых сейчас ждут, — иначе взаимное ожидание двух сервисов выглядит как молчаливое
зависание. Сторож работает через player loop; `waitWarningSeconds: 0` его выключает.

Скоупы конкурентного контейнера делят реестр промисов, поэтому регистрация в родителе будит ждущих
в потомке. Разбуженный ожидающий перепроверяет видимость по своей цепочке и при промахе ждёт дальше.
По той же причине `CancelPendingWaits()` из потомка гасит ожидания на всём дереве.

## Жизненный цикл

```csharp
container.Clear();       // очистить регистрации
container.DisposeAll();  // очистить и вызвать Dispose
```

`DisposeAll` вызывает `Dispose` ровно один раз на инстанс, даже если он зарегистрирован под
несколькими ключами, и не роняет очистку на исключении одного сервиса.

## Производительность

Рефлексия по типу считается один раз и кэшируется в `InjectionCache` (`ConcurrentDictionary`, общий
на процесс). В точке инъекции уже лежат готовые тип и способ записи — на каждой инъекции switch по
`MemberInfo` не выполняется. Первая инъекция в новый тип всё ещё идёт по полной рефлексии:
критичные сцены прогревайте заранее вызовом `InjectionCache.GetInjectPoints(typeof(T))`.

## API

| Тип | Назначение |
|---|---|
| `DependenciesContainer` | Однопоточный контейнер на `Dictionary` |
| `ConcurrentDependenciesContainer` | Потокобезопасный контейнер с ожиданием регистраций |
| `IDependencyContainer` | Полный контракт: чтение, запись, скоупы, инъекция |
| `IDependencyResolver` | Только чтение — принимайте его там, где не регистрируете |
| `IDependencyRegistry` | Только запись |
| `IAsyncDependencyResolver` | `GetAsync` / `WhenRegistered` |
| `InjectAttribute` / `ProvideAttribute` | `[Inject]` и `[Provide]` |
| `DuplicateRegistrationPolicy` | Поведение при повторной регистрации |
| `MissingDependencyResult` | Ответ обработчика отсутствующей зависимости |
| `MissingDependency` / `ProvideConflict` | Результаты `TryInject` и `TryValidateProvide` |
| `AsyncInjector` | `InjectAsync`, `WhenInjectableAsync` |
| `Injector` | Синхронная инъекция поверх любого резолвера |
| `InjectionCache` | Кэш рефлексии, прогрев |
| `IDiLogger` / `UnityDiLogger` / `NullDiLogger` | Точка выхода диагностики |
| `UnityContainers` | Фабрика контейнеров с логгером Unity |
