# Changelog

## [2.0.0]

Ломающий релиз. Совместимости с 1.x нет.

### Исправлено

- Переопределённое свойство с `[Inject]` внедрялось дважды: точка находилась и в наследнике, и в базе.
  Теперь дедупликация по `GetBaseDefinition()`.
- `Add(Type, obj)` не проверял совместимость инстанса с ключом — ошибка всплывала позже кастом в `Get<T>`.
  Теперь проверка `IsInstanceOfType` на регистрации.
- Инъекция в value-type молча ничего не делала: значение боксировалось, изменения терялись. Теперь исключение.
- `[Inject]` на статическом члене молча игнорировался. Теперь исключение.
- `TryGet<T>` возвращал `false`, когда тип зарегистрирован, но не приводится, — пряталась ошибка регистрации.
  Инвариант держится проверкой на регистрации, поэтому `TryGet` возвращает `false` только при отсутствии.
- `package.json` заявлял Unity 2020.3, хотя код требовал C# 9. Минимум поднят до 2021.3.
- Кэш рефлексии не был потокобезопасным.

### Изменено

- `[Inject]` на `readonly`-поле разрешён и считается рекомендуемым стилем: контейнер пишет поле
  рефлексией, компилятор запрещает перезапись из пользовательского кода. Статические `initonly`
  по-прежнему отклоняются — там рефлексия действительно не работает.
- Ядро вынесено в сборку без ссылок на движок (`noEngineReferences: true`). `Debug` живёт только в `Exerussus.DI.Unity`.
- Логирование через `IDiLogger`, передаётся в конструктор. Статического хука больше нет.
- Вся логика инъекции — в stateless-статике `Injector` поверх интерфейсов, общая для обоих контейнеров.
- `TryInjectFields` → `Inject` (бросает) и `TryInject` (возвращает `MissingDependency`).
- `TryProvideFields` → `Provide`.
- `ThrowIfHasProvideFields(target, message)` → `ValidateProvide` / `TryValidateProvide`.
- Параметр `fallback` заменён на полноценные скоупы: `Parent` и `CreateScope()`.
- Кортеж `(bool isError, object instance)` заменён на `MissingDependencyResult` с явными `Fail`/`Skip`/`Use`/`Register`.
  «Подставить» и «зарегистрировать» больше не одно и то же.
- `Add(object)` убран как источник неоднозначности перегрузок. Вместо него `Add<T>(T)` и `AddByRuntimeType(object)`.
- Поведение при дубле настраивается через `DuplicateRegistrationPolicy`.
- `GetAllRefs()` → `Registrations` (`IReadOnlyDictionary`, обёртка создаётся один раз, LINQ убран).
- `Inject` и `TryInject` транзакционны: при нехватке зависимости объект не заполняется частично.
- Имя сборки `Exerussus.Di` → `Exerussus.DI`, проставлен `rootNamespace`.

### Добавлено

- `ConcurrentDependenciesContainer` — потокобезопасный контейнер с `GetAsync`/`WhenRegistered`/`InjectAsync`
  и сторожем зависаний.
- `DisposeAll()` — очистка с `Dispose` ровно один раз на инстанс.
- `Get(Type)`, `TryGet(Type, out object)`, `TryRemove<T>()`, `HasOwn<T>()`.

## [1.0.2]

Исходная версия: один класс `DependenciesContainer`, атрибуты `[Inject]`/`[Provide]`.
