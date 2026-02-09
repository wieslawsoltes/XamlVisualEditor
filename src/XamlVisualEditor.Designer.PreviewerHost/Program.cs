using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace XamlVisualEditor.Designer.PreviewerHost;

public static class Program
{
    public static void Main(string[] args)
    {
        string targetAssemblyPath = ResolveTargetAssemblyPath(args);
        TargetAssemblyResolver resolver = new(targetAssemblyPath);
        AssemblyLoadContext.Default.Resolving += resolver.Resolve;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += resolver.ResolveUnmanaged;

        _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(targetAssemblyPath);

        RuntimeXamlLoaderRegistrar.Configure(targetAssemblyPath);
        RuntimeXamlLoaderRegistrar.Register();

        Assembly designerSupportAssembly = LoadAssemblyByName(resolver, "Avalonia.DesignerSupport");
        Type entryPointType = designerSupportAssembly.GetType(
            "Avalonia.DesignerSupport.Remote.RemoteDesignerEntryPoint",
            throwOnError: true)
            ?? throw new InvalidOperationException("Unable to locate RemoteDesignerEntryPoint type.");

        MethodInfo? mainMethod = entryPointType.GetMethod(
            "Main",
            BindingFlags.Public | BindingFlags.Static);
        if (mainMethod == null)
        {
            throw new InvalidOperationException("Unable to locate RemoteDesignerEntryPoint.Main method.");
        }

        _ = mainMethod.Invoke(null, new object[] { args });
    }

    private static Assembly LoadAssemblyByName(TargetAssemblyResolver resolver, string assemblyName)
    {
        string? path = resolver.ResolveAssemblyPath(new AssemblyName(assemblyName));
        if (path != null)
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
    }

    private static string ResolveTargetAssemblyPath(string[] args)
    {
        if (args.Length == 0)
        {
            throw new InvalidOperationException("Previewer host requires a target assembly path.");
        }

        for (int i = args.Length - 1; i >= 0; i--)
        {
            string candidate = args[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!HasAssemblyExtension(candidate))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        throw new InvalidOperationException("Unable to locate target assembly path in previewer arguments.");
    }

    private static bool HasAssemblyExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TargetAssemblyResolver
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _targetDirectory;

        public TargetAssemblyResolver(string mainAssemblyPath)
        {
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
            _targetDirectory = Path.GetDirectoryName(mainAssemblyPath) ?? string.Empty;
        }

        public Assembly? Resolve(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            string? path = ResolveAssemblyPath(assemblyName);
            return path != null ? context.LoadFromAssemblyPath(path) : null;
        }

        public IntPtr ResolveUnmanaged(Assembly assembly, string unmanagedDllName)
        {
            string? path = ResolveUnmanagedPath(unmanagedDllName);
            return path != null
                ? NativeLibrary.Load(path)
                : IntPtr.Zero;
        }

        public string? ResolveAssemblyPath(AssemblyName assemblyName)
        {
            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            if (path == null && !string.IsNullOrWhiteSpace(_targetDirectory))
            {
                string candidate = Path.Combine(_targetDirectory, assemblyName.Name + ".dll");
                if (File.Exists(candidate))
                {
                    path = candidate;
                }
            }

            return path;
        }

        public string? ResolveUnmanagedPath(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path == null && !string.IsNullOrWhiteSpace(_targetDirectory))
            {
                string candidate = Path.Combine(_targetDirectory, unmanagedDllName);
                if (File.Exists(candidate))
                {
                    path = candidate;
                }
            }

            return path;
        }
    }
}

internal static class RuntimeXamlLoaderRegistrar
{
    private static string? s_targetDirectory;

    public static void Configure(string targetAssemblyPath)
    {
        s_targetDirectory = Path.GetDirectoryName(targetAssemblyPath);
    }

    public static void Register()
    {
        RegisterFileAssetLoaderFallback();

        Type loaderInterface = Type.GetType(
            "Avalonia.Markup.Xaml.AvaloniaXamlLoader+IRuntimeXamlLoader, Avalonia.Markup.Xaml")
            ?? throw new InvalidOperationException(
                "Unable to locate Avalonia runtime XAML loader interface.");

        object proxy = DispatchProxy.Create(loaderInterface, typeof(RuntimeXamlLoaderProxy));
        RuntimeXamlLoaderProxy loaderProxy = (RuntimeXamlLoaderProxy)proxy;
        loaderProxy.Handler = LoadRuntimeXaml;

        object locator = GetCurrentMutable();
        MethodInfo bindMethod = FindBindMethod(locator.GetType());
        object helper = bindMethod.MakeGenericMethod(loaderInterface)
            .Invoke(locator, null)
            ?? throw new InvalidOperationException("Failed to bind runtime XAML loader.");

        MethodInfo? toConstant = helper.GetType().GetMethod(
            "ToConstant",
            BindingFlags.Instance | BindingFlags.Public);
        if (toConstant == null)
        {
            throw new InvalidOperationException(
                "Unable to locate ToConstant on AvaloniaLocator registration helper.");
        }

        if (toConstant.IsGenericMethodDefinition)
        {
            toConstant = toConstant.MakeGenericMethod(proxy.GetType());
        }

        _ = toConstant.Invoke(helper, new[] { proxy });
    }

    private class RuntimeXamlLoaderProxy : DispatchProxy
    {
        public Func<object, object, object>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Load" && args?.Length == 2
                && args[0] != null && args[1] != null)
            {
                if (Handler == null)
                {
                    throw new InvalidOperationException("Runtime XAML loader handler not configured.");
                }

                object document = args[0]!;
                object configuration = args[1]!;
                return Handler(document, configuration);
            }

            throw new NotSupportedException("Unsupported runtime XAML loader invocation.");
        }
    }

    private class AssetLoaderProxy : DispatchProxy
    {
        public object? Inner { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
            {
                throw new InvalidOperationException("Asset loader proxy invoked without a target method.");
            }

            if (args == null || args.Length == 0)
            {
                return Inner != null ? targetMethod.Invoke(Inner, args) : null;
            }

            Uri? uri = args[0] as Uri;
            Uri? baseUri = args.Length > 1 ? args[1] as Uri : null;
            Uri resolved = ResolveUri(uri, baseUri);

            if (resolved.IsAbsoluteUri && resolved.Scheme == Uri.UriSchemeFile)
            {
                if (TryHandleFileScheme(targetMethod, resolved, out object? result))
                {
                    return result;
                }
            }

            return Inner != null ? targetMethod.Invoke(Inner, args) : null;
        }

        private static bool TryHandleFileScheme(
            MethodInfo targetMethod,
            Uri resolved,
            out object? result)
        {
            string localPath = resolved.LocalPath;
            switch (targetMethod.Name)
            {
                case "Exists":
                    result = File.Exists(localPath);
                    return true;
                case "Open":
                    result = File.OpenRead(localPath);
                    return true;
                case "OpenAndGetAssembly":
                    result = CreateOpenAndGetAssemblyResult(targetMethod.ReturnType, localPath);
                    return true;
                case "GetAssets":
                    result = Array.Empty<Uri>();
                    return true;
                default:
                    result = null;
                    return false;
            }
        }

        private static object? CreateOpenAndGetAssemblyResult(Type returnType, string localPath)
        {
            Stream stream = File.OpenRead(localPath);
            Assembly assembly = typeof(RuntimeXamlLoaderRegistrar).Assembly;
            try
            {
                return Activator.CreateInstance(returnType, stream, assembly);
            }
            catch
            {
                return null;
            }
        }

        private static Uri ResolveUri(Uri? uri, Uri? baseUri)
        {
            if (uri == null)
            {
                return baseUri ?? new Uri("urn:missing");
            }

            if (uri.IsAbsoluteUri)
            {
                return uri;
            }

            if (baseUri != null)
            {
                return new Uri(baseUri, uri);
            }

            return uri;
        }
    }

    private sealed class AssetLoaderServiceProvider : IServiceProvider
    {
        private readonly Type _assetLoaderType;
        private readonly object? _assetLoader;

        public AssetLoaderServiceProvider(Type assetLoaderType, object? assetLoader)
        {
            _assetLoaderType = assetLoaderType;
            _assetLoader = assetLoader;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == _assetLoaderType
                || _assetLoaderType.IsAssignableFrom(serviceType)
                || serviceType.IsAssignableFrom(_assetLoaderType)
                || string.Equals(serviceType.FullName, _assetLoaderType.FullName, StringComparison.Ordinal))
            {
                return _assetLoader;
            }

            return null;
        }
    }

    private static object LoadRuntimeXaml(object document, object configuration)
    {
        Assembly? localAssembly = GetLocalAssembly(configuration);
        Assembly? resolvedAssembly = EnsureLocalAssemblyInContext(localAssembly);
        if (resolvedAssembly != null)
        {
            SetLocalAssembly(configuration, resolvedAssembly);
        }

        if (TryLoadWithRuntimeLoader(document, configuration, out object? result))
        {
            return result ?? throw new InvalidOperationException(
                "AvaloniaRuntimeXamlLoader.Load returned null.");
        }

        return LoadWithAvaloniaXamlLoader(document);
    }

    private static bool TryLoadWithRuntimeLoader(
        object document,
        object configuration,
        out object? result)
    {
        result = null;
        Type? loaderType = GetRuntimeXamlLoaderType();
        if (loaderType == null)
        {
            return false;
        }

        MethodInfo loadMethod = FindLoadMethod(loaderType);
        result = loadMethod.Invoke(null, new[] { document, configuration });
        if (result == null)
        {
            throw new InvalidOperationException("AvaloniaRuntimeXamlLoader.Load returned null.");
        }

        return true;
    }

    private static Type? GetRuntimeXamlLoaderType()
    {
        const string typeName = "Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader";

        Type? loaderType = Type.GetType(typeName + ", Avalonia.Markup.Xaml");
        if (loaderType != null)
        {
            return loaderType;
        }

        loaderType = Type.GetType(typeName + ", Avalonia.Markup.Xaml.Loader");
        if (loaderType != null)
        {
            return loaderType;
        }

        loaderType = FindTypeInLoadedAssemblies(typeName);
        if (loaderType != null)
        {
            return loaderType;
        }

        EnsureMarkupXamlAssembliesLoaded();
        loaderType = FindTypeInLoadedAssemblies(typeName);
        if (loaderType != null)
        {
            return loaderType;
        }

        loaderType = TryLoadTypeFromAssembly(typeName, "Avalonia.Markup.Xaml")
            ?? TryLoadTypeFromAssembly(typeName, "Avalonia.Markup.Xaml.Loader")
            ?? FindTypeInLoadedAssemblies(typeName);
        return loaderType;
    }

    private static void EnsureMarkupXamlAssembliesLoaded()
    {
        if (string.IsNullOrWhiteSpace(s_targetDirectory))
        {
            return;
        }

        AssemblyLoadContext? context =
            AssemblyLoadContext.GetLoadContext(typeof(RuntimeXamlLoaderRegistrar).Assembly);
        if (context == null)
        {
            return;
        }

        foreach (string path in Directory.GetFiles(s_targetDirectory, "Avalonia.Markup.Xaml*.dll"))
        {
            try
            {
                _ = context.LoadFromAssemblyPath(path);
            }
            catch
            {
                // Ignore load failures and continue scanning.
            }
        }
    }

    private static Type? FindTypeInLoadedAssemblies(string typeName)
    {
        AssemblyLoadContext? context =
            AssemblyLoadContext.GetLoadContext(typeof(RuntimeXamlLoaderRegistrar).Assembly);
        if (context == null)
        {
            return null;
        }

        foreach (Assembly assembly in context.Assemblies)
        {
            Type? type = assembly.GetType(typeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static Type? TryLoadTypeFromAssembly(string typeName, string assemblyName)
    {
        Type? type = GetTypeFromAssembly(typeName, assemblyName);
        if (type != null)
        {
            return type;
        }

        AssemblyLoadContext? context =
            AssemblyLoadContext.GetLoadContext(typeof(RuntimeXamlLoaderRegistrar).Assembly);
        if (context == null || string.IsNullOrWhiteSpace(s_targetDirectory))
        {
            return null;
        }

        string candidate = Path.Combine(s_targetDirectory, assemblyName + ".dll");
        if (!File.Exists(candidate))
        {
            return null;
        }

        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(candidate);
            return assembly.GetType(typeName, throwOnError: false);
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo FindLoadMethod(Type loaderType)
    {
        MethodInfo[] methods = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (MethodInfo method in methods)
        {
            if (method.Name == "Load" && method.GetParameters().Length == 2)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Unable to locate AvaloniaRuntimeXamlLoader.Load method.");
    }

    private static object LoadWithAvaloniaXamlLoader(object document)
    {
        Type loaderType = GetAvaloniaXamlLoaderType();
        string xaml = GetXamlFromDocument(document)
            ?? throw new InvalidOperationException("Runtime XAML document does not contain Xaml text.");
        Uri? baseUri = GetUriProperty(document, "BaseUri") ?? GetUriProperty(document, "Uri");
        string tempPath = CreateTempXamlFile(xaml);
        Uri sourceUri = new(tempPath);
        Uri effectiveBaseUri = baseUri ?? sourceUri;
        IServiceProvider? serviceProvider = CreateAssetLoaderServiceProvider();

        object? result = TryInvokeAvaloniaXamlLoaderWithUris(
            loaderType,
            sourceUri,
            effectiveBaseUri,
            serviceProvider);
        if (result != null)
        {
            return result;
        }

        result = TryInvokeAvaloniaXamlLoader(loaderType, xaml, baseUri, document);
        if (result != null)
        {
            return result;
        }

        string methods = DescribeAvaloniaXamlLoaderMethods(loaderType);
        throw new InvalidOperationException(
            "Unable to locate AvaloniaXamlLoader.Load method. Available overloads: " + methods);
    }

    private static Type GetAvaloniaXamlLoaderType()
    {
        Type? type = GetTypeFromAssembly("Avalonia.Markup.Xaml.AvaloniaXamlLoader", "Avalonia.Markup.Xaml")
            ?? GetTypeFromAssembly("Avalonia.Markup.Xaml.AvaloniaXamlLoader", "Avalonia.Markup");
        return type
            ?? throw new InvalidOperationException("Unable to locate AvaloniaXamlLoader type.");
    }

    private static object? TryInvokeAvaloniaXamlLoader(
        Type loaderType,
        string xaml,
        Uri? baseUri,
        object document)
    {
        foreach (MethodInfo methodDefinition in loaderType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            if (methodDefinition.Name != "Load")
            {
                continue;
            }

            MethodInfo? method = TryCloseGenericMethod(methodDefinition);
            if (method == null)
            {
                continue;
            }

            if (!TryBuildAvaloniaXamlLoaderArguments(
                method,
                xaml,
                baseUri,
                document,
                out object[]? args))
            {
                continue;
            }

            object? target = null;
            if (!method.IsStatic)
            {
                target = TryCreateLoaderInstance(loaderType);
                if (target == null)
                {
                    continue;
                }
            }

            object? result = method.Invoke(target, args);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static object? TryInvokeAvaloniaXamlLoaderWithUris(
        Type loaderType,
        Uri sourceUri,
        Uri baseUri,
        IServiceProvider? serviceProvider)
    {
        MethodInfo[] methods = loaderType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        object? result = TryInvokeWithServiceProvider(methods, sourceUri, baseUri, serviceProvider, loaderType);
        if (result != null)
        {
            return result;
        }

        return TryInvokeWithServiceProvider(methods, sourceUri, baseUri, null, loaderType);
    }

    private static object? TryInvokeWithServiceProvider(
        MethodInfo[] methods,
        Uri sourceUri,
        Uri baseUri,
        IServiceProvider? serviceProvider,
        Type loaderType)
    {
        foreach (MethodInfo methodDefinition in methods)
        {
            if (methodDefinition.Name != "Load")
            {
                continue;
            }

            if (serviceProvider != null && !HasServiceProviderParameter(methodDefinition))
            {
                continue;
            }

            MethodInfo? method = TryCloseGenericMethod(methodDefinition);
            if (method == null)
            {
                continue;
            }

            if (!TryBuildUriLoaderArguments(method, sourceUri, baseUri, serviceProvider, out object[]? args))
            {
                continue;
            }

            object? target = null;
            if (!method.IsStatic)
            {
                target = TryCreateLoaderInstance(loaderType);
                if (target == null)
                {
                    continue;
                }
            }

            object? result = method.Invoke(target, args);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static bool HasServiceProviderParameter(MethodInfo method)
    {
        foreach (ParameterInfo parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(IServiceProvider))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildUriLoaderArguments(
        MethodInfo method,
        Uri sourceUri,
        Uri baseUri,
        IServiceProvider? serviceProvider,
        out object[]? args)
    {
        args = null;
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length < 2)
        {
            return false;
        }

        object[] values = new object[parameters.Length];
        bool sourceAssigned = false;
        bool baseAssigned = false;

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType == typeof(Uri))
            {
                if (!sourceAssigned)
                {
                    values[i] = sourceUri;
                    sourceAssigned = true;
                    continue;
                }

                if (!baseAssigned)
                {
                    values[i] = baseUri;
                    baseAssigned = true;
                    continue;
                }

                return false;
            }

            if (parameterType == typeof(IServiceProvider))
            {
                values[i] = serviceProvider ?? null!;
                continue;
            }

            if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null)
            {
                values[i] = null!;
                continue;
            }

            return false;
        }

        if (!sourceAssigned || !baseAssigned)
        {
            return false;
        }

        args = values;
        return true;
    }

    private static string CreateTempXamlFile(string xaml)
    {
        string fileName = "xveditor_" + Guid.NewGuid().ToString("N") + ".axaml";
        string path = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(path, xaml, System.Text.Encoding.UTF8);
        return path;
    }


    private static MethodInfo? TryCloseGenericMethod(MethodInfo methodDefinition)
    {
        if (!methodDefinition.IsGenericMethodDefinition)
        {
            return methodDefinition;
        }

        Type[] genericArgs = methodDefinition.GetGenericArguments();
        if (genericArgs.Length == 0)
        {
            return methodDefinition;
        }

        Type[] selected = new Type[genericArgs.Length];
        for (int i = 0; i < genericArgs.Length; i++)
        {
            Type[] constraints = genericArgs[i].GetGenericParameterConstraints();
            if (constraints.Length > 0)
            {
                selected[i] = constraints[0];
                continue;
            }

            selected[i] = typeof(object);
        }

        try
        {
            return methodDefinition.MakeGenericMethod(selected);
        }
        catch
        {
            return null;
        }
    }

    private static object? TryCreateLoaderInstance(Type loaderType)
    {
        try
        {
            return Activator.CreateInstance(loaderType, nonPublic: true);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeAvaloniaXamlLoaderMethods(Type loaderType)
    {
        MethodInfo[] methods = loaderType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        List<string> entries = new();
        foreach (MethodInfo method in methods)
        {
            if (method.Name != "Load")
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            string[] paramNames = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                paramNames[i] = parameters[i].ParameterType.Name;
            }

            entries.Add(method.Name + "(" + string.Join(", ", paramNames) + ")");
        }

        return entries.Count == 0 ? "<none>" : string.Join("; ", entries);
    }

    private static bool TryBuildAvaloniaXamlLoaderArguments(
        MethodInfo method,
        string xaml,
        Uri? baseUri,
        object document,
        out object[]? args)
    {
        args = null;
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return false;
        }

        bool stringAssigned = false;

        object[] values = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType == typeof(string))
            {
                if (stringAssigned)
                {
                    return false;
                }

                values[i] = xaml;
                stringAssigned = true;
                continue;
            }

            if (parameterType == typeof(Uri))
            {
                values[i] = baseUri ?? new Uri("urn:designer");
                continue;
            }

            if (parameterType == typeof(IServiceProvider))
            {
                values[i] = null!;
                continue;
            }

            if (parameterType == typeof(Assembly))
            {
                Assembly? assembly = GetAssemblyProperty(document, "LocalAssembly")
                    ?? GetAssemblyProperty(document, "Assembly")
                    ?? GetAssemblyProperty(document, "OwnerAssembly");
                if (assembly == null)
                {
                    return false;
                }

                values[i] = assembly;
                continue;
            }

            if (parameterType.IsInstanceOfType(document))
            {
                values[i] = document;
                continue;
            }

            if (parameterType == typeof(bool))
            {
                values[i] = false;
                continue;
            }

            if (parameterType == typeof(CancellationToken))
            {
                values[i] = default(CancellationToken);
                continue;
            }

            if (parameterType.IsEnum)
            {
                values[i] = Activator.CreateInstance(parameterType) ??
                    Enum.GetValues(parameterType).GetValue(0)!;
                continue;
            }

            if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null)
            {
                values[i] = null!;
                continue;
            }

            return false;
        }

        if (!stringAssigned)
        {
            return false;
        }

        args = values;
        return true;
    }

    private static Assembly? GetAssemblyProperty(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(instance) as Assembly;
    }

    private static string? GetRequiredStringProperty(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(instance) as string;
    }

    private static Uri? GetUriProperty(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(instance) as Uri;
    }

    private static string? GetXamlFromDocument(object document)
    {
        string? xaml = GetRequiredStringProperty(document, "Xaml")
            ?? GetRequiredStringProperty(document, "Text")
            ?? GetRequiredStringProperty(document, "Content");
        if (!string.IsNullOrWhiteSpace(xaml))
        {
            return xaml;
        }

        object? nested = GetPropertyValue(document, "Document");
        if (nested != null)
        {
            xaml = GetRequiredStringProperty(nested, "Xaml")
                ?? GetRequiredStringProperty(nested, "Text")
                ?? GetRequiredStringProperty(nested, "Content");
            if (!string.IsNullOrWhiteSpace(xaml))
            {
                return xaml;
            }
        }

        return ReadXamlFromStream(document)
            ?? ReadXamlFromBytes(document);
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(instance);
    }

    private static string? ReadXamlFromStream(object document)
    {
        object? streamValue = GetPropertyValue(document, "Stream")
            ?? GetPropertyValue(document, "XamlStream")
            ?? GetPropertyValue(document, "ContentStream");
        if (streamValue is not Stream stream)
        {
            return null;
        }

        try
        {
            long originalPosition = stream.CanSeek ? stream.Position : 0;
            using StreamReader reader = new(stream, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true);
            string xaml = reader.ReadToEnd();
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }

            return string.IsNullOrWhiteSpace(xaml) ? null : xaml;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadXamlFromBytes(object document)
    {
        object? bytesValue = GetPropertyValue(document, "Bytes")
            ?? GetPropertyValue(document, "Data");
        if (bytesValue is byte[] bytes)
        {
            string xaml = System.Text.Encoding.UTF8.GetString(bytes);
            return string.IsNullOrWhiteSpace(xaml) ? null : xaml;
        }

        return null;
    }

    private static Assembly? GetLocalAssembly(object configuration)
    {
        PropertyInfo? property = configuration.GetType().GetProperty(
            "LocalAssembly",
            BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(configuration) as Assembly;
    }

    private static void SetLocalAssembly(object configuration, Assembly assembly)
    {
        PropertyInfo? property = configuration.GetType().GetProperty(
            "LocalAssembly",
            BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(configuration, assembly);
        }
    }

    private static Assembly? EnsureLocalAssemblyInContext(Assembly? localAssembly)
    {
        if (localAssembly == null)
        {
            return null;
        }

        AssemblyLoadContext? targetContext =
            AssemblyLoadContext.GetLoadContext(typeof(RuntimeXamlLoaderRegistrar).Assembly);
        AssemblyLoadContext? currentContext = AssemblyLoadContext.GetLoadContext(localAssembly);
        if (targetContext == null || currentContext == targetContext)
        {
            return localAssembly;
        }

        string? assemblyPath = localAssembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return localAssembly;
        }

        foreach (Assembly loaded in targetContext.Assemblies)
        {
            if (string.Equals(loaded.Location, assemblyPath, StringComparison.OrdinalIgnoreCase))
            {
                return loaded;
            }
        }

        try
        {
            return targetContext.LoadFromAssemblyPath(assemblyPath);
        }
        catch
        {
            return localAssembly;
        }
    }

    private static object GetCurrentMutable()
    {
        Type? locatorType = GetTypeFromAssembly("Avalonia.AvaloniaLocator", "Avalonia")
            ?? GetTypeFromAssembly("Avalonia.AvaloniaLocator", "Avalonia.Base");
        if (locatorType == null)
        {
            throw new InvalidOperationException("Unable to locate AvaloniaLocator type.");
        }
        PropertyInfo? property = locatorType.GetProperty(
            "CurrentMutable",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object? locator = property?.GetValue(null);
        if (locator == null)
        {
            throw new InvalidOperationException("Unable to resolve AvaloniaLocator.CurrentMutable.");
        }

        return locator;
    }

    private static object GetCurrent()
    {
        Type? locatorType = GetTypeFromAssembly("Avalonia.AvaloniaLocator", "Avalonia")
            ?? GetTypeFromAssembly("Avalonia.AvaloniaLocator", "Avalonia.Base");
        if (locatorType == null)
        {
            throw new InvalidOperationException("Unable to locate AvaloniaLocator type.");
        }
        PropertyInfo? property = locatorType.GetProperty(
            "Current",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object? locator = property?.GetValue(null);
        if (locator == null)
        {
            throw new InvalidOperationException("Unable to resolve AvaloniaLocator.Current.");
        }

        return locator;
    }

    private static IServiceProvider? CreateAssetLoaderServiceProvider()
    {
        Type? assetLoaderType = GetTypeFromAssembly("Avalonia.Platform.IAssetLoader", "Avalonia.Base")
            ?? GetTypeFromAssembly("Avalonia.Platform.IAssetLoader", "Avalonia");
        if (assetLoaderType == null)
        {
            return null;
        }

        object? proxy = CreateFileAssetLoaderProxy(assetLoaderType);
        return new AssetLoaderServiceProvider(assetLoaderType, proxy);
    }

    private static object? CreateFileAssetLoaderProxy(Type assetLoaderType)
    {
        object locator = GetCurrent();
        object? existing = TryGetService(locator, assetLoaderType);
        object proxy = DispatchProxy.Create(assetLoaderType, typeof(AssetLoaderProxy));
        AssetLoaderProxy loaderProxy = (AssetLoaderProxy)proxy;
        loaderProxy.Inner = existing;
        return proxy;
    }

    private static void RegisterFileAssetLoaderFallback()
    {
        Type? assetLoaderType = GetTypeFromAssembly("Avalonia.Platform.IAssetLoader", "Avalonia.Base")
            ?? GetTypeFromAssembly("Avalonia.Platform.IAssetLoader", "Avalonia");
        if (assetLoaderType == null)
        {
            return;
        }

        object? proxy = CreateFileAssetLoaderProxy(assetLoaderType);
        if (proxy == null)
        {
            return;
        }

        object mutableLocator = GetCurrentMutable();
        MethodInfo bindMethod = FindBindMethod(mutableLocator.GetType());
        object helper = bindMethod.MakeGenericMethod(assetLoaderType)
            .Invoke(mutableLocator, null)
            ?? throw new InvalidOperationException("Failed to bind asset loader.");

        MethodInfo? toConstant = helper.GetType().GetMethod(
            "ToConstant",
            BindingFlags.Instance | BindingFlags.Public);
        if (toConstant == null)
        {
            throw new InvalidOperationException("Unable to locate ToConstant on AvaloniaLocator helper.");
        }

        if (toConstant.IsGenericMethodDefinition)
        {
            toConstant = toConstant.MakeGenericMethod(proxy.GetType());
        }

        _ = toConstant.Invoke(helper, new[] { proxy });
    }

    private static object? TryGetService(object locator, Type serviceType)
    {
        MethodInfo[] methods = locator.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (MethodInfo method in methods)
        {
            if (method.Name == "GetService"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 1
                && method.GetParameters().Length == 0)
            {
                return method.MakeGenericMethod(serviceType).Invoke(locator, null);
            }
        }

        foreach (MethodInfo method in methods)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == "GetService"
                && parameters.Length == 1
                && parameters[0].ParameterType == typeof(Type))
            {
                return method.Invoke(locator, new object[] { serviceType });
            }
        }

        return null;
    }

    private static Type? GetTypeFromAssembly(string typeName, string assemblyName)
    {
        Type? type = Type.GetType(typeName + ", " + assemblyName);
        if (type != null)
        {
            return type;
        }

        AssemblyLoadContext? context =
            AssemblyLoadContext.GetLoadContext(typeof(RuntimeXamlLoaderRegistrar).Assembly);
        if (context == null)
        {
            return null;
        }

        try
        {
            Assembly assembly = context.LoadFromAssemblyName(new AssemblyName(assemblyName));
            type = assembly.GetType(typeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }
        catch
        {
            // Ignore load failures; try path-based loading instead.
        }

        if (!string.IsNullOrWhiteSpace(s_targetDirectory))
        {
            string candidate = Path.Combine(s_targetDirectory, assemblyName + ".dll");
            if (File.Exists(candidate))
            {
                try
                {
                    Assembly assembly = context.LoadFromAssemblyPath(candidate);
                    type = assembly.GetType(typeName, throwOnError: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // Ignore and fall through to assembly scan.
                }
            }
        }

        foreach (Assembly assembly in context.Assemblies)
        {
            type = assembly.GetType(typeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static MethodInfo FindBindMethod(Type locatorType)
    {
        MethodInfo[] methods = locatorType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (MethodInfo method in methods)
        {
            if (method.Name == "Bind"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 1
                && method.GetParameters().Length == 0)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Unable to locate AvaloniaLocator.Bind<T>() method.");
    }
}
