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

    internal class ServerClientAppender : ClientAppenderBase
    {
        protected override void ClientAppenderImpl(Type element, RtInterface result, TypeResolver resolver)
        {
            var options = SignalRGenerationOptions.All[element];
            var hub = options.HubPath;
            var typeName = element.IsInterface && element.Name.StartsWith('I') ? element.Name.Substring(1) : element.Name;
            var clientImpl = new RtClass()
            {
                
                Name = new RtSimpleTypeName($"{typeName}Invoker"), //Naming becomes hard with a strongly typed "client" API on the server already
                Export = true,
                Decorators = {  },
                Implementees = { result.Name },
                Members =
                {
                    new RtField
                    {
                        AccessModifier = AccessModifier.Private,
                        Identifier = new RtIdentifier("hubConnection"),
                        Type = new RtSimpleTypeName("Promise<HubConnection>")
                    },
                    new RtConstructor
                    {
                        Arguments = { new RtArgument
                        {
                            Type = new RtSimpleTypeName(options.HubConnectionProviderType),
                            Identifier = new RtIdentifier("hubConnectionProvider")
                        }},
                        Body = new RtRaw($"this.hubConnection = hubConnectionProvider.getHubConnection(\"{hub}\");"),
                        LineAfter = " "
                    }
                },
            };
            clientImpl.Members.AddRange(GetImplementationMembers(result));

            Context.Location.CurrentNamespace.CompilationUnits.Add(clientImpl);
            Context.AddNewLine();

        }

        private IEnumerable<RtNode> GetImplementationMembers(RtInterface result)
        {
            var functions = result.Members.OfType<RtFunction>();
            foreach (var function in functions)
            {
                var arguments = function.Arguments.Select(a => a.Identifier.ToString());
                var fullParameters = arguments.Prepend($"\"{function.Identifier.IdentifierName}\"");
                function.Body = new RtRaw($"return this.hubConnection.then(hub => hub.invoke({string.Join(", ", fullParameters)}));");
                function.LineAfter = " ";
            }

            return functions;
        }
    }
}
