using Reinforced.Typings;
using Reinforced.Typings.Ast;
using Reinforced.Typings.Ast.TypeNames;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SignalRTypeScriptHubGenerator
{
	internal class FrontEndClientAppender : ClientAppenderBase
	{
		protected override bool ReturnExisting()
		{
			return false;
		}

		protected override void ClientAppenderImpl(Type element, RtInterface result, TypeResolver resolver)
		{
			SignalRGenerationOptions options = SignalRGenerationOptions.All[element];
			string typeName = element.IsInterface && element.Name.StartsWith('I') ? element.Name.Substring(1) : element.Name;

			Context.AddNewLine();
			string callbacks = $"{typeName}_Callbacks";
			RtInterface eventInter = new RtInterface
			{
				Name = new RtSimpleTypeName(callbacks),
				Members = {

				},
				Export = false
			};
			eventInter.Members.AddRange(result.Members.OfType<RtFunction>().Select(m => new RtField() { AccessModifier = AccessModifier.Public, Identifier = m.Identifier, Type = GetCallbackType(m) }));
			Context.Location.CurrentNamespace.CompilationUnits.Add(eventInter);
			Context.AddNewLine();

			Context.AddRawToNamespace($"export type {typeName}_CallbackNames = keyof {callbacks};").AddNewLine();
			Context.AddRawToNamespace($"export type {typeName}_Callback<TKey extends {typeName}_CallbackNames> = {callbacks}[TKey];").AddNewLine();
		}

		private void BuildClientClass(string typeName, SignalRGenerationOptions options, Type element, RtInterface result, TypeResolver resolver)
		{
			RtClass clientImpl = new RtClass
			{
				Name = new RtSimpleTypeName(typeName),
				Export = true,
				Decorators = { },
				Members =
				{
					new RtConstructor
					{
						Arguments = { new RtArgument
						{
							Type = new RtSimpleTypeName(options.HubConnectionProviderType),
							Identifier = new RtIdentifier("hubConnectionProvider")
						}},
						Body = new RtRaw($"this.hubConnection = hubConnectionProvider.getHubConnection(\"{options.HubPath}\");"),
						LineAfter = " "
					}
				}
			};
			clientImpl.Members.AddRange(GetImplementationMembers(result, options.HubPath));

			Context.Location.CurrentNamespace.CompilationUnits.Add(clientImpl);
			Context.AddNewLine();
		}

		private RtDelegateType GetCallbackType(RtFunction function)
		{
			return new RtDelegateType(function.Arguments.ToArray(), new RtSimpleTypeName("void"));
		}

		private IEnumerable<RtNode> GetImplementationMembers(RtInterface result, string hub)
		{
			List<RtNode> members = new List<RtNode>();
			IEnumerable<RtFunction> functions = result.Members.OfType<RtFunction>();
			foreach (RtFunction function in functions)
			{

				List<string> rtTypeNames = function.Arguments.Select(a => a.Type.ToString()).ToList();
				string generics = string.Join(",", rtTypeNames);
				if (rtTypeNames.Count > 1)
				{
					generics = $"[{generics}]";
				}
				string stringType = rtTypeNames.Count == 0 ? "() => void" : $"SimpleEventDispatcher<{generics}>";

				RtField eventDispatcher = new RtField
				{
					AccessModifier = AccessModifier.Public,
					Identifier = new RtIdentifier($"on{function.Identifier.ToString().FirstCharToUpper()}"),
					Type = new RtSimpleTypeName(stringType),
					InitializationExpression = $"new {stringType}()"
				};
				members.Add(eventDispatcher);
			}

			return members;
		}

		private RtRaw GetEventRegistrationBody(IEnumerable<RtFunction> functions, string hub)
		{
			string pre = $"hubConnectionProvider.getHubConnection(\"{hub}\").then(hubConnection => {{\r\n";
			IEnumerable<string> e = functions.Select(f =>
			{
				List<string> args = f.Arguments.Select(a => $"{a.Identifier} : {a.Type}").ToList();
				string response = $"({string.Join(",", args)})";
				string commaArgs = string.Join(",", f.Arguments.Select(a => a.Identifier));
				if (args.Count > 1)
				{
					commaArgs = $"[{commaArgs}]";
				}
				return $"  hubConnection.on(\"{f.Identifier}\", {response} => {{\r\n      console.log(\"{f.Identifier} received from server\", {commaArgs});\r\n      this.on{f.Identifier.ToString().FirstCharToUpper()}.dispatch({commaArgs});\r\n    }});";
			});
			string post = "\r\n});";
			return new RtRaw(pre + string.Join("\r\n", e) + post);
		}
	}
}
