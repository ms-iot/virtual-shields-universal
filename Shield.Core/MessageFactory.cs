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

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage;
using Newtonsoft.Json;
using Shield.Core.Models;

namespace Shield.Core
{
    public static class MessageFactory
    {
        private static Dictionary<string, Type> serviceClasses;

        public static void LoadClasses()
        {
            if (serviceClasses == null)
            {
                LoadServices();
            }
        }

        private static void LoadServices()
        {
            var assy = typeof (MessageFactory).GetTypeInfo().Assembly;

            var assemblies = new List<Assembly> {assy}; // await GetAssemblyListAsync();}
            Dictionary<string, Type> dictionary = new Dictionary<string, Type>();

            foreach (Assembly a in assemblies)
                foreach (Type type in a.ExportedTypes)
                {
                    if (type.GetTypeInfo().GetCustomAttributes<Service>().Any())
                        foreach (Service attribute in type.GetTypeInfo().GetCustomAttributes<Service>())
                            dictionary.Add(attribute.Id, type);
                }

            serviceClasses = dictionary;
        }

        private static async Task<IEnumerable<Assembly>> GetAssemblyListAsync()
        {
            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            List<Assembly> assemblies = new List<Assembly>();
            foreach (StorageFile file in await folder.GetFilesAsync())
            {
                if (file.FileType == ".winmd")
                {
                    AssemblyName name = new AssemblyName() { Name = file.Path };
                    try
                    {
                        Assembly asm = Assembly.Load(name);
                        assemblies.Add(asm);
                    }
                    catch (Exception)
                    {
                        //ignore move on
                    }
                }
            }

            return assemblies;
        }

        private static MessageBase Create<T>(string data) where T:MessageBase 
        {
            var result = JsonConvert.DeserializeObject<T>(data);
            result._Source = data;
            return result;
        }

        private static MessageBase Create(string data, Type type)
        {
            var result = (MessageBase) Convert.ChangeType(JsonConvert.DeserializeObject(data, type), type);
            result._Source = data;
            return result;
        }

        public static MessageBase FromMessage(string data)
        {
            MessageBase json;

            try
            {
                json = JsonConvert.DeserializeObject<MessageBase>(data);
            }
            catch (Exception)
            {
                return null;
            }

            json._Source = data;

            if (json.Service == null)
            {
                return null;
            }

            if (!serviceClasses.ContainsKey(json.Service))
            {
                return json;
            }

            return Create(data, serviceClasses[json.Service]);
        } 
    }
}
