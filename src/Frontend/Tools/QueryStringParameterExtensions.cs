﻿using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PrimeView.Frontend.Tools
{
  public static class QueryStringParameterExtensions
  {
    // Apply the values from the query string to the current component
    public static void SetParametersFromQueryString<T>(this T component, NavigationManager navigationManager, ISyncLocalStorageService localStorage)
        where T : ComponentBase
    {
      if (!Uri.TryCreate(navigationManager.Uri, UriKind.RelativeOrAbsolute, out var uri))
        return;

      // Parse the query string
      Dictionary<string, StringValues> queryString = QueryHelpers.ParseQuery(uri.Query);
      Dictionary<string, string> storedParameters = null;

      string storageKey = GetLocalStorageKey<T>();

      if (localStorage.ContainKey(storageKey))
      {
        try
        {
          storedParameters = localStorage.GetItem<Dictionary<string, string>>(storageKey);
        }
        catch
        {
          localStorage.RemoveItem(storageKey);
        }
      }

      if (storedParameters == null)
        storedParameters = new();

      // Enumerate all properties of the component
      foreach (var property in GetProperties<T>())
      {
        // Get the name of the parameter to read from the query string
        var parameterName = GetQueryStringParameterName(property);
        if (parameterName == null)
          continue; // The property is not decorated by [QueryStringParameterAttribute]

				if (!queryString.TryGetValue(parameterName, out StringValues value) && storedParameters.ContainsKey(parameterName))
					value = storedParameters[parameterName];

				if (!StringValues.IsNullOrEmpty(value))
        {
          // Convert the value from string to the actual property type
          var convertedValue = ConvertValue(value, property.PropertyType);
          property.SetValue(component, convertedValue);
        }
      }
    }

    // Apply the values from the component to the query string
    public static void UpdateQueryString<T>(this T component, NavigationManager navigationManager, ISyncLocalStorageService localStorage, IJSInProcessRuntime runtime)
        where T : ComponentBase
    {
      if (!Uri.TryCreate(navigationManager.Uri, UriKind.RelativeOrAbsolute, out var uri))
        uri = new Uri(navigationManager.Uri);

      // Fill the dictionary with the parameters of the component
      Dictionary<string, StringValues> parameters = QueryHelpers.ParseQuery(uri.Query);
      Dictionary<string, string> storedParameters = new();

      foreach (var property in GetProperties<T>())
      {
        var parameterName = GetQueryStringParameterName(property);
        if (parameterName == null)
          continue;

        var value = property.GetValue(component);
        if (value is null)
        {
          parameters.Remove(parameterName);
        }
        else
        {
          var convertedValue = ConvertToString(value);
          parameters[parameterName] = convertedValue;
          storedParameters[parameterName] = convertedValue;
        }
      }

      // Compute the new URL
      var newUri = uri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.UriEscaped);
      foreach (var parameter in parameters)
      {
        foreach (var value in parameter.Value)
        {
          newUri = QueryHelpers.AddQueryString(newUri, parameter.Key, value);
        }
      }

      runtime.InvokeVoid("PrimeViewJS.ShowUrl", newUri);
      localStorage.SetItem(GetLocalStorageKey<T>(), storedParameters);
    }

    private static string GetLocalStorageKey<T>()
      => $"{typeof(T).Name}QueryParameters";

    private static object ConvertValue(StringValues value, Type type)
    {
      try
      {
        return Convert.ChangeType(value[0], type, CultureInfo.InvariantCulture);
      }
      catch { }

      return GetDefault(type);
    }

    private static object GetDefault(Type type)
      => type.IsValueType ? Activator.CreateInstance(type) : null;

    private static string ConvertToString(object value)
      => Convert.ToString(value, CultureInfo.InvariantCulture);

    private static PropertyInfo[] GetProperties<T>()
      => typeof(T).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
 
    private static string GetQueryStringParameterName(PropertyInfo property)
    {
      var attribute = property.GetCustomAttribute<QueryStringParameterAttribute>();
      if (attribute == null)
        return null;

      return attribute.Name ?? property.Name;
    }
  }
}
