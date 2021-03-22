using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Reinforced.Typings;
using Reinforced.Typings.Ast;
using Reinforced.Typings.Ast.TypeNames;
using Reinforced.Typings.Fluent;
using Reinforced.Typings.Generators;

namespace SignalRTypeScriptHubGenerator
{

    internal abstract class ClientAppenderBase : InterfaceCodeGenerator
    {
        public override RtInterface GenerateNode(Type element, RtInterface result, TypeResolver resolver)
        {
            var existing = base.GenerateNode(element, result, resolver);
            if (existing == null) return null;

            if (Context.Location.CurrentNamespace == null) return existing;

            ClientAppenderImpl(element, result, resolver);

            return ReturnExisting() ? existing : null;
        }

        protected virtual bool ReturnExisting()
        {
            return true;
        }

        protected abstract void ClientAppenderImpl(Type element, RtInterface result, TypeResolver resolver);
    }
}
