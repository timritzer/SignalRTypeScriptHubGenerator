using System;
using Reinforced.Typings;
using Reinforced.Typings.Ast;

namespace SignalRTypeScriptHubGenerator
{
	internal static class ContextExtensions
	{
		public static ExportContext AddNewLine(this ExportContext context)
		{
			return context.AddRawToNamespace(Environment.NewLine);
		}

		public static ExportContext AddRawToNamespace(this ExportContext context, string rawString)
		{
			context.Location.CurrentNamespace.CompilationUnits.Add(new RtRaw(rawString));
			return context;
		}
	}
}
