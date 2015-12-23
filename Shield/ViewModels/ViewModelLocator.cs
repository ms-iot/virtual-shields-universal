/*
    Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.

    The MIT License(MIT)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

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
            IocContainer.Container.RegisterType(typeof (IMainViewModel), typeof (MainViewModel),
                new object[] {}, new ContainerControlledLifetimeManager());
        }
    }

    public class IocContainer
    {
        public static IocContainer Container = new IocContainer();
        private readonly Dictionary<Type, object> objects = new Dictionary<Type, object>();

        public static T Get<T>()
        {
            return (T) ((ContainerControlledLifetimeManager) Container.objects[typeof (T)]).GetValue();
        }

        public void RegisterType(Type interfaceType, Type realType, object[] parms,
            ContainerControlledLifetimeManager containerControlledLifetimeManager)
        {
            var instance = Activator.CreateInstance(realType, parms);
            containerControlledLifetimeManager.SetValue(instance);
            objects.Add(interfaceType, containerControlledLifetimeManager);
        }
    }
}