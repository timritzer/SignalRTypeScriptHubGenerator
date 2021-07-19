using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reinforced.Typings;
using Reinforced.Typings.Ast;
using Reinforced.Typings.Ast.TypeNames;

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
				Decorators = { },
				Implementees = { result.Name },
				Members =
				{
					new RtField
					{
						AccessModifier = AccessModifier.Private,
						Identifier = new RtIdentifier("hubConnection"),
						Type = new RtSimpleTypeName("Promise<HubConnection>")
					},
					new RtField
					{
						AccessModifier = AccessModifier.Private,
						Identifier = new RtIdentifier("queue"),
						Type = new RtSimpleTypeName("PromiseResolver<unknown>[]"),
					},
					new RtField
					{
						AccessModifier = AccessModifier.Private,
						Identifier = new RtIdentifier("OFFLINE_QUEUE_INTERVAL_SECONDS"),
						Type = new RtSimpleTypeName("number"),
						InitializationExpression = "5"
					},
					new RtField
					{
						AccessModifier = AccessModifier.Private,
						Identifier = new RtIdentifier("MAX_QUEUE_COUNT"),
						Type = new RtSimpleTypeName("number"),
						InitializationExpression = "100"
					},
					new RtField
					{
						AccessModifier = AccessModifier.Private,
						Identifier = new RtIdentifier("QUEUE_TIMEOUT_SECONDS"),
						Type = new RtSimpleTypeName("number"),
						InitializationExpression = "60"
					},
					new RtConstructor
					{
						Arguments = { new RtArgument
						{
							Type = new RtSimpleTypeName(options.HubConnectionProviderType),
							Identifier = new RtIdentifier("hubConnectionProvider")
						}},
						Body = new RtRaw(
							$"this.hubConnection = hubConnectionProvider.getHubConnection(\"{hub}\");" + Environment.NewLine +
							"this.queue = [];" + Environment.NewLine +
							"	window.setTimeout(() => {" + Environment.NewLine +
							$"		console.debug(\"starting {typeName} queue handler\");" + Environment.NewLine +
							"		this.handleQueue();" + Environment.NewLine +
							"	} , this.OFFLINE_QUEUE_INTERVAL_SECONDS * 1000);" + Environment.NewLine
						),
						LineAfter = " "
					},
					new RtRaw(Encoding.Default.GetString(Scripts.Scripts.handleQueue).Replace("{{typeName}}", typeName)),
					new RtRaw(Encoding.Default.GetString(Scripts.Scripts.invoke).Replace("{{typeName}}", typeName))
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
				var hubInvoke = $"hub.invoke({string.Join(", ", fullParameters)})";
				var body = "const hub = await this.hubConnection;" + Environment.NewLine +
					$"return this.invoke(() => {hubInvoke}, hub, \"{function.Identifier.IdentifierName}\")";
				function.Body = new RtRaw(body);
				function.LineAfter = " ";
			}

			return functions;
		}
	}
}
