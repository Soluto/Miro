using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Miro.Services.Logger;
using Miro.Services.MiroConfig;
using Serilog;

namespace Miro.Services.Utils
{
    public static class DictionaryExt
    {
        public static T ToObject<T>(this Dictionary<string, string> dict)
        {
            return (T)GetObject(dict, typeof(T));
        }

          private static Object GetObject(this Dictionary<string, string> dict, Type type)
        {
            var obj = Activator.CreateInstance(type);
            var allProperties = type.GetProperties();

            foreach (var kv in dict)
            {
                 foreach (var prop in allProperties)
                 {
                     if (prop.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                     {
                          object value = kv.Value;
                          if (prop.PropertyType == typeof(Boolean))
                          {
                              prop.SetValue(obj, (string) value == "true" ? true : false, null);
                          }
                          else 
                          {
                            prop.SetValue(obj, value, null);
                          }
                     } 
                 }
            }
            return obj;
        }
    }
}