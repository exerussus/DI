using System;

namespace Exerussus.DI
{
    /// <summary> Полный контейнер: чтение, запись, скоупы и инъекция. </summary>
    public interface IDependencyContainer : IDependencyResolver, IDependencyRegistry
    {
        /// <summary> Дочерний скоуп: свои регистрации плюс всё, что видно у родителя. </summary>
        IDependencyContainer CreateScope();

        void Merge(IDependencyResolver other);
        void Merge(IDependencyResolver other, DuplicateRegistrationPolicy policy);

        /// <summary> Заполняет [Inject]-члены. Транзакционно: при нехватке зависимости объект не трогается. </summary>
        void Inject(object target);

        bool TryInject(object target, out MissingDependency missing);

        /// <summary> Инъекция с обработчиком отсутствующих типов. Не транзакционна. </summary>
        void Inject(object target, Func<Type, MissingDependencyResult> onMissing);

        /// <summary> Публикует [Provide]-члены объекта в контейнер. </summary>
        void Provide(object target);
        void Provide(object target, DuplicateRegistrationPolicy policy);

        /// <summary> Бросает, если [Provide]-член конфликтует с уже зарегистрированным типом. </summary>
        void ValidateProvide(object target);
        bool TryValidateProvide(object target, out ProvideConflict conflict);
    }
}
