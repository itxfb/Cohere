using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Cohere.Api.Controllers.v1
{
    public class MvcHelper
    {
        public static string GetControllerMethodsNames()
        {
            List<Type> cmdtypes = GetSubClasses<ControllerBase>();
            var controlersInfo = string.Empty;
            foreach (Type ctrl in cmdtypes)
            {
                var methodsInfo = string.Empty;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                MemberInfo[] methodName = ctrl.GetMethods(flags);
                foreach (MemberInfo method in methodName)
                {
                    if (method.DeclaringType.ToString() == ctrl.UnderlyingSystemType.ToString())
                    {
                        methodsInfo += "<li><i>" + method.Name + "</i></li>";
                    }
                }

                controlersInfo += "<li>" + ctrl.Name.Replace("Controller", string.Empty) + "<ul>" + methodsInfo + "</ul></li>";
            }

            return controlersInfo;
        }

        private static List<Type> GetSubClasses<T>()
        {
            return Assembly.GetCallingAssembly().GetTypes().Where(
                type => type.IsSubclassOf(typeof(T))).ToList();
        }
    }
}