# Exerussus.DI

Простой DI-контейнер для Unity. Регистрация инстансов и инъекция через атрибуты на полях и свойствах.

## Установка

Через Unity Package Manager → *Add package from git URL*:

```
https://github.com/exerussus/DI.git
```

## Использование

### Регистрация

```csharp
var container = new DependenciesContainer();

container.Add(new PlayerService());            // по фактическому типу
container.Add<IPlayerService>(new PlayerService()); // по интерфейсу
container.Add(serviceA, serviceB, serviceC);   // пачкой
```

### Инъекция

```csharp
public class Game
{
    [Inject] private IPlayerService _player;
    [Inject] public IInputService Input { get; set; }
}

var game = new Game();
container.TryInjectFields(game);
```

Если зависимость не найдена — бросается исключение. Поддерживаются приватные поля и свойства, в том числе унаследованные.

Для опциональной/ленивой инъекции есть перегрузка с фабрикой:

```csharp
container.TryInjectFields(game, type =>
{
    if (type == typeof(IOptionalService)) return (false, null); // не ошибка, оставить пустым
    return (false, Activator.CreateInstance(type));             // создать и закэшировать
});
```

### Provide — публикация полей в контейнер

```csharp
public class Bootstrap
{
    [Provide] private IPlayerService _player = new PlayerService();
    [Provide] private IInputService _input = new InputService();
}

container.TryProvideFields(new Bootstrap());
// теперь _player и _input доступны через container.Get<...>()
```

## API

| Метод | Назначение |
|---|---|
| `Add(obj)` / `Add<T>(obj)` / `Add(params)` | Регистрация |
| `Remove<T>()` / `Remove(Type)` | Удаление |
| `Get<T>()` | Получение, исключение при отсутствии |
| `TryGet<T>(out value)` | Безопасное получение |
| `Has<T>()` / `Has(Type)` | Проверка наличия |
| `Merge(other)` | Слияние с другим контейнером |
| `Clear()` | Очистка |
| `TryInjectFields(target, fallback?)` | Инъекция в `[Inject]`-поля |
| `TryProvideFields(target)` | Публикация `[Provide]`-полей |
| `ThrowIfHasProvideFields(target, msg)` | Проверка на конфликты перед `Provide` |

## Ограничения

- Только синглтоны (готовые инстансы). Нет lifetime-скоупов и фабрик из коробки.
- Не потокобезопасен — рассчитан на главный поток Unity.
- `Clear()` не вызывает `Dispose()` у сервисов.
- Рефлексия кэшируется, но первая инъекция в новый тип идёт по полной — критичные сцены лучше прогревать заранее.
