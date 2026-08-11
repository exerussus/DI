#if !UNITY_2020_3_OR_NEWER
// Харнесс собирается вне Unity. Внутри редактора файл должен быть невидим:
// иначе он попадёт в Assembly-CSharp и уедет в плеерный билд.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Exerussus.DI.Tests
{
    internal sealed class CheckFailedException : Exception
    {
        public CheckFailedException(string message) : base(message) { }
    }

    internal static class Check
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new CheckFailedException(message);
        }

        public static void False(bool condition, string message)
        {
            if (condition) throw new CheckFailedException(message);
        }

        public static void Same(object expected, object actual, string message)
        {
            if (!ReferenceEquals(expected, actual)) throw new CheckFailedException(message);
        }

        public static void Equal(object expected, object actual, string message)
        {
            if (!Equals(expected, actual)) throw new CheckFailedException($"{message}: ожидалось {expected}, получено {actual}");
        }

        public static void Null(object value, string message)
        {
            if (value != null) throw new CheckFailedException(message);
        }

        public static void NotNull(object value, string message)
        {
            if (value == null) throw new CheckFailedException(message);
        }

        public static void Throws<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception exception)
            {
                throw new CheckFailedException($"{message}: ожидалось {typeof(TException).Name}, получено {exception.GetType().Name}");
            }

            throw new CheckFailedException($"{message}: исключение {typeof(TException).Name} не брошено");
        }
    }

    internal static class Program
    {
        private static int Main()
        {
            var methods = new List<MethodInfo>();

            foreach (var method in typeof(Suite).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name.StartsWith("Test_", StringComparison.Ordinal)) methods.Add(method);
            }

            methods.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

            var passed = 0;
            var failures = new List<string>();

            foreach (var method in methods)
            {
                try
                {
                    method.Invoke(null, null);
                    passed++;
                }
                catch (TargetInvocationException wrapper)
                {
                    failures.Add($"{method.Name}: {wrapper.InnerException?.Message ?? wrapper.Message}");
                }
                catch (Exception exception)
                {
                    failures.Add($"{method.Name}: {exception.Message}");
                }
            }

            Console.WriteLine($"пройдено {passed}/{methods.Count}");

            foreach (var failure in failures) Console.WriteLine($"  ПАДЕНИЕ  {failure}");

            return failures.Count == 0 ? 0 : 1;
        }
    }

    /// <summary> Регрессии на дефекты версии 1.x. </summary>
    internal static class Suite
    {
        // ---------- Проверка типа на регистрации ----------

        public static void Test_Add_InstanceNotAssignableToKey_Throws()
        {
            var container = new DependenciesContainer();
            Check.Throws<ArgumentException>(() => container.Add(typeof(IServiceA), new ServiceB()),
                "Несовместимый инстанс должен отклоняться на регистрации");
        }

        public static void Test_Add_ByInterface_Resolves()
        {
            var container = new DependenciesContainer();
            var service = new ServiceA();

            container.Add<IServiceA>(service);

            Check.Same(service, container.Get<IServiceA>(), "Сервис должен доставаться по интерфейсу");
            Check.False(container.Has<ServiceA>(), "Регистрация по интерфейсу не должна создавать ключ по классу");
        }

        // ---------- Двойная инъекция в переопределённое свойство ----------

        public static void Test_Inject_OverriddenProperty_SetterCalledOnce()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var target = new DerivedWithOverride();
            container.Inject(target);

            Check.Equal(1, target.SetCount, "Свойство помечено в базе и в наследнике — сеттер должен сработать один раз");
        }

        public static void Test_Inject_PrivateFieldOfBaseClass_IsFilled()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var target = new DerivedWithPrivateBaseField();
            container.Inject(target);

            Check.NotNull(target.BaseService, "Приватное поле базового класса должно заполняться");
        }

        // ---------- Транзакционность ----------

        public static void Test_Inject_MissingDependency_LeavesTargetUntouched()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var target = new NeedsTwoServices();

            Check.Throws<InvalidOperationException>(() => container.Inject(target), "Нехватка зависимости должна бросать");
            Check.Null(target.A, "При нехватке зависимости объект не должен остаться наполовину заполненным");
        }

        public static void Test_TryInject_MissingDependency_ReportsIt()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var success = container.TryInject(new NeedsTwoServices(), out var missing);

            Check.False(success, "TryInject должен вернуть false");
            Check.Equal(typeof(IServiceB), missing.MissingType, "Должен быть указан недостающий тип");
        }

        public static void Test_Inject_ReadonlyField_Injected()
        {
            var container = new DependenciesContainer();
            var service = new ServiceA();
            container.Add<IServiceA>(service);

            var target = new ReadonlyFieldTarget();
            container.Inject(target);

            Check.Same(service, target.Service, "readonly-поле должно заполняться");
        }

        // ---------- Неподдерживаемые члены ----------

        public static void Test_Inject_IntoValueType_Throws()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            Check.Throws<ArgumentException>(() => container.Inject(new StructTarget()),
                "Инъекция в value-type должна отклоняться");
        }

        public static void Test_Inject_StaticField_Throws()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            Check.Throws<InvalidOperationException>(() => container.Inject(new StaticFieldTarget()),
                "Статический член должен отклоняться");
        }

        public static void Test_Inject_PropertyWithoutSetter_Throws()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            Check.Throws<InvalidOperationException>(() => container.Inject(new GetterOnlyTarget()),
                "Свойство без сеттера должно отклоняться");
        }

        // ---------- Скоупы ----------

        public static void Test_Scope_ResolvesFromParent()
        {
            var root = new DependenciesContainer();
            root.Add<IServiceA>(new ServiceA());

            var scope = root.CreateScope();

            Check.True(scope.Has<IServiceA>(), "Скоуп должен видеть родительскую регистрацию");
            Check.False(scope.HasOwn<IServiceA>(), "Родительская регистрация не должна считаться собственной");
        }

        public static void Test_Scope_LocalRegistrationOverridesParent()
        {
            var root = new DependenciesContainer();
            var parentService = new ServiceA();
            var scopeService = new ServiceA();
            root.Add<IServiceA>(parentService);

            var scope = root.CreateScope();
            scope.Add(typeof(IServiceA), scopeService);

            Check.Same(scopeService, scope.Get<IServiceA>(), "Локальная регистрация должна перекрывать родительскую");
            Check.Same(parentService, root.Get<IServiceA>(), "Родитель не должен меняться");
        }

        // ---------- Политики дублей ----------

        public static void Test_Add_DuplicateWithThrowPolicy_Throws()
        {
            var container = new DependenciesContainer(null, null, DuplicateRegistrationPolicy.Throw);
            container.Add<IServiceA>(new ServiceA());

            Check.Throws<InvalidOperationException>(() => container.Add<IServiceA>(new ServiceA()),
                "Политика Throw должна бросать на дубле");
        }

        public static void Test_Add_DuplicateWithIgnorePolicy_KeepsFirst()
        {
            var container = new DependenciesContainer(null, null, DuplicateRegistrationPolicy.Ignore);
            var first = new ServiceA();
            container.Add<IServiceA>(first);
            container.Add<IServiceA>(new ServiceA());

            Check.Same(first, container.Get<IServiceA>(), "Политика Ignore должна сохранять первую регистрацию");
        }

        // ---------- Provide ----------

        public static void Test_Provide_PublishesMembers()
        {
            var container = new DependenciesContainer();
            container.Provide(new Bootstrap());

            Check.True(container.Has<IServiceA>(), "Provide должен опубликовать первый член");
            Check.True(container.Has<IServiceB>(), "Provide должен опубликовать второй член");
        }

        public static void Test_Provide_NullMember_Throws()
        {
            var container = new DependenciesContainer();
            Check.Throws<InvalidOperationException>(() => container.Provide(new BootstrapWithNull()),
                "Provide с null должен бросать");
        }

        public static void Test_TryValidateProvide_ReportsConflict()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var valid = container.TryValidateProvide(new Bootstrap(), out var conflict);

            Check.False(valid, "Конфликт должен быть обнаружен");
            Check.Equal(typeof(IServiceA), conflict.ConflictingType, "Должен быть указан конфликтующий тип");
        }

        // ---------- Жизненный цикл ----------

        public static void Test_DisposeAll_DisposesSharedInstanceOnce()
        {
            var container = new DependenciesContainer();
            var service = new DisposableService();
            container.Add<IServiceA>(service);
            container.Add<DisposableService>(service);

            container.DisposeAll();

            Check.Equal(1, service.DisposeCount, "Dispose должен вызваться один раз на инстанс");
            Check.False(container.Has<IServiceA>(), "После DisposeAll контейнер должен быть пуст");
        }

        public static void Test_OnMissing_Register_CachesInstance()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var target = new NeedsTwoServices();
            container.Inject(target, type => type == typeof(IServiceB)
                ? MissingDependencyResult.Register(new ServiceB())
                : MissingDependencyResult.Fail());

            Check.NotNull(target.B, "Созданный инстанс должен подставиться");
            Check.True(container.Has<IServiceB>(), "Register должен положить инстанс в контейнер");
        }

        public static void Test_OnMissing_Skip_LeavesMemberEmpty()
        {
            var container = new DependenciesContainer();
            container.Add<IServiceA>(new ServiceA());

            var target = new NeedsTwoServices();
            container.Inject(target, _ => MissingDependencyResult.Skip());

            Check.NotNull(target.A, "Найденная зависимость должна заполниться");
            Check.Null(target.B, "Skip должен оставить член пустым");
            Check.False(container.Has<IServiceB>(), "Skip не должен ничего регистрировать");
        }

        public static void Test_Logger_ReceivesDuplicateWarning()
        {
            var logger = new RecordingLogger();
            var container = new DependenciesContainer(null, logger);

            container.Add<IServiceA>(new ServiceA());
            container.Add<IServiceA>(new ServiceA());

            Check.Equal(1, logger.Warnings.Count, "Политика Warn должна дать ровно одно предупреждение");
        }

        // ---------- Фикстуры ----------

        private interface IServiceA { }
        private interface IServiceB { }

        private class ServiceA : IServiceA { }
        private class ServiceB : IServiceB { }

        private sealed class RecordingLogger : IDiLogger
        {
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public void Warning(string message) => Warnings.Add(message);
            public void Error(string message) => Errors.Add(message);
        }

        private class DisposableService : IServiceA, IDisposable
        {
            public int DisposeCount;
            public void Dispose() => DisposeCount++;
        }

        private class BaseWithVirtual
        {
            [Inject] public virtual IServiceA Service { get; set; }
        }

        private class DerivedWithOverride : BaseWithVirtual
        {
            private IServiceA _service;
            public int SetCount;

            [Inject]
            public override IServiceA Service
            {
                get => _service;
                set
                {
                    _service = value;
                    SetCount++;
                }
            }
        }

        private class BaseWithPrivateField
        {
            [Inject] private IServiceA _service;
            public IServiceA BaseService => _service;
        }

        private class DerivedWithPrivateBaseField : BaseWithPrivateField { }

        private class NeedsTwoServices
        {
            [Inject] private IServiceA _a;
            [Inject] private IServiceB _b;

            public IServiceA A => _a;
            public IServiceB B => _b;
        }

        private struct StructTarget
        {
            [Inject] public IServiceA Service;
        }

        private class ReadonlyFieldTarget
        {
            [Inject] private readonly IServiceA _service = null;
            public IServiceA Service => _service;
        }

        private class StaticFieldTarget
        {
            [Inject] private static IServiceA _service;
            public static IServiceA Service => _service;
        }

        private class GetterOnlyTarget
        {
            [Inject] public IServiceA Service { get; }
        }

        private class Bootstrap
        {
            [Provide] private readonly IServiceA _a = new ServiceA();
            [Provide] private readonly IServiceB _b = new ServiceB();
        }

        private class BootstrapWithNull
        {
            [Provide] private readonly IServiceA _a = null;
        }
    }
}
#endif
