using System;
using System.Collections.Generic;
using Microsoft.Practices.Unity;

namespace Shield.ViewModels
{
    public sealed class ViewModelLocator
    {
        public ViewModelLocator()
        {
            RegisterIoCBindings();
        }

        public IMainViewModel MainViewModel => IocContainer.Get<IMainViewModel>();

        private void RegisterIoCBindings()
        {
            IocContainer.Container.RegisterType(typeof(IMainViewModel), typeof(MainViewModel),
                new object[] {}, new ContainerControlledLifetimeManager());
        }
    }

    public class IocContainer
    {
        public static IocContainer Container = new IocContainer();
        private Dictionary<Type, object> objects = new Dictionary<Type, object>();

        public static T Get<T>()
        {
            return (T) ((ContainerControlledLifetimeManager) Container.objects[typeof (T)]).GetValue();
        }

        public void RegisterType(Type interfaceType, Type realType, object[] parms, 
            ContainerControlledLifetimeManager containerControlledLifetimeManager)
        {
            var instance = Activator.CreateInstance(realType, parms);
            containerControlledLifetimeManager.SetValue( instance );
            objects.Add(interfaceType, containerControlledLifetimeManager);
        }
    }
}