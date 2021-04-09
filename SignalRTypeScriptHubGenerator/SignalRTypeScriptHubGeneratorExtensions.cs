using Reinforced.Typings.Ast.TypeNames;
using Reinforced.Typings.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SignalRTypeScriptHubGenerator
{
	public class SignalRGenerationOptions
	{
		/// <summary>
		/// The type to import to provide the hub connection for SignalR
		/// </summary>
		public string HubConnectionProviderType { get; set; }
		/// <summary>
		/// The file/module to import to provide the hub connection for SignalR
		/// </summary>
		public string HubConnectionProviderModule { get; set; }
		/// <summary>
		/// Provide a filter to the types to include in the generation. Performs a "StartsWith" filter on the namespaces if provided.
		/// Defaults to not filtered.
		/// </summary>
		public string NamespaceTypeFilter { get; set; } = "";

		public string HubPath { get; set; } = "hub/";

		//Don't love this, but don't see a good way to pass this to the generators
		public static Dictionary<Type, SignalRGenerationOptions> All = new Dictionary<Type, SignalRGenerationOptions>();

	}

	public static class SignalRTypeScriptHubGeneratorExtensions
	{

		/// <summary>
		/// Add code generation for SignalR Hubs and clients (Strongly typed).
		/// Requires module pattern, and auto async. Will enable them automatically.
		/// </summary>
		/// <typeparam name="THub"></typeparam>
		/// <typeparam name="TClient"></typeparam>
		/// <param name="builder"></param>
		/// <param name="options"></param>
		/// <param name="namespaceFilter"></param>
		public static void GenerateSignalRTypeScriptHub<THub, TClient>(
			this ConfigurationBuilder builder,
			SignalRGenerationOptions options)
		{
			Type serverType = typeof(THub);
			Type frontendType = typeof(TClient);

			//Cache options so they can be accessed in the generators.
			SignalRGenerationOptions.All[typeof(THub)] = options;
			SignalRGenerationOptions.All[typeof(TClient)] = options;

			builder.Global(c =>
			{
				c.UseModules().AutoAsync(true);
			});

			builder.AddImport("{ HubConnection }", "@microsoft/signalr");
			builder.AddImport($"{{ {options.HubConnectionProviderType} }}", options.HubConnectionProviderModule);

			builder.Substitute(typeof(DateTime), new RtSimpleTypeName("string"));
			builder.Substitute(typeof(Uri), new RtSimpleTypeName("string"));

			HashSet<Type> relatedTypes = new HashSet<Type>();
			relatedTypes.UnionWith(TraverseTypes(serverType, options.NamespaceTypeFilter));
			relatedTypes.UnionWith(TraverseTypes(frontendType, options.NamespaceTypeFilter));
			relatedTypes.Remove(serverType);
			relatedTypes.Remove(frontendType);

			IEnumerable<Type> relatedClassTypes = relatedTypes.Where(t => t.IsClass);
			IEnumerable<Type> relatedEnumTypes = relatedTypes.Where(t => t.IsEnum);
			IEnumerable<Type> otherTypes = relatedTypes.Where(t => !(t.IsClass || t.IsEnum));

			Func<Type, int> orderFunc = relatedTypes.SelectMany(t => EnumerateHierarchy(t, e => e.BaseType).Reverse()).ToList().IndexOf;

			builder.ExportAsEnums(relatedEnumTypes, c => c.Order(orderFunc(c.Type)));
			builder.ExportAsInterfaces(relatedClassTypes, c => c.WithPublicProperties().WithPublicFields().WithPublicMethods().Order(orderFunc(c.Type)));
			builder.ExportAsInterfaces(otherTypes, c => c.WithPublicProperties().WithPublicFields().WithPublicMethods().Order(orderFunc(c.Type)));
			builder.ExportAsInterface<THub>().WithPublicProperties().WithPublicFields().WithPublicMethods().WithCodeGenerator<ServerClientAppender>();
			builder.ExportAsInterface<TClient>().WithPublicProperties().WithPublicFields().WithPublicMethods().WithCodeGenerator<FrontEndClientAppender>();
		}

		private class SignalRConfiguration { public string HubPath { get; set; } }

		private static IEnumerable<T> EnumerateHierarchy<T>(T item, Func<T, T> selector) where T : class
		{
			do
			{
				yield return item;
				item = selector(item);
			}
			while (item != default(T));
		}

		public static IEnumerable<Type> TraverseTypes(Type type, string namespaceFilter)
		{
			return TraverseTypes(type, namespaceFilter, new List<Type>());
		}

		public static IEnumerable<Type> TraverseTypes(Type type, string namespaceFilter, List<Type> visited)
		{
			visited.Add(type);
			HashSet<Type> types = new HashSet<Type>();
			types.UnionWith(type.GetInterfaces());
			types.UnionWith(GetBaseClassIfAny(type));
			types.UnionWith(TraverseMethods(type));
			types.UnionWith(TraverseProperties(type));
			types.UnionWith(types.ToList().SelectMany(t => visited.Contains(t) ? new List<Type>() : TraverseTypes(t, namespaceFilter, visited)));
			types.IntersectWith(types.Where(t => t.Namespace != null && t.Namespace.StartsWith(namespaceFilter)));
			types.ExceptWith(types.ToList().Where(t => t.IsByRef || t.IsArray));
			types.Add(type);
			return types;
		}

		private static IEnumerable<Type> GetBaseClassIfAny(Type type)
		{
			return type.BaseType == null ? Array.Empty<Type>() : new[] { type.BaseType };
		}

		private static IEnumerable<Type> TraverseProperties(Type type)
		{
			return type.GetProperties().SelectMany(HandleProperty);
		}

		private static IEnumerable<Type> TraverseMethods(Type type)
		{
			HashSet<Type> types = new HashSet<Type>();
			types.UnionWith(type.GetMethods().SelectMany(HandleReturnType));
			types.UnionWith(type.GetMethods().SelectMany(m => m.GetParameters().SelectMany(HandleParameters)));
			return types;
		}

		private static IEnumerable<Type> HandleParameters(ParameterInfo p)
		{
			HashSet<Type> types = new HashSet<Type> { p.ParameterType };
			if (p.ParameterType.IsGenericType)
			{
				types.UnionWith(p.ParameterType.GetGenericArguments());
			}

			return types;
		}

		private static IEnumerable<Type> HandleReturnType(MethodInfo m)
		{
			HashSet<Type> types = new HashSet<Type> { m.ReturnType };
			if (m.ReturnType.IsGenericType)
			{
				types.UnionWith(m.ReturnType.GetGenericArguments());
			}

			return types;
		}

		private static IEnumerable<Type> HandleProperty(PropertyInfo p)
		{
			HashSet<Type> types = new HashSet<Type> { p.PropertyType };
			if (p.PropertyType.IsGenericType)
			{
				types.UnionWith(p.PropertyType.GetGenericArguments());
			}

			return types;
		}
	}
}
