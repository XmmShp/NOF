using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("NOF.Contract.Tests")]

namespace NOF;

/// <summary />
// ReSharper disable once InconsistentNaming
public static partial class __NOF_Contract_Extensions__
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.NOFDefaults;
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <param name="httpClient">The HTTP client instance. Must not be null.</param>
    extension(HttpClient httpClient)
    {
        /// <summary>
        /// Sends an HTTP request using the specified method to the given endpoint with the provided request object.
        /// If the method supports a body (POST, PUT, PATCH), the request is serialized as JSON.
        /// Otherwise, properties of the request object are encoded as query parameters.
        /// </summary>
        /// <param name="method">The HTTP method to use. Must not be null.</param>
        /// <param name="endpoint">The URI endpoint (relative or absolute). Must not be null or empty.</param>
        /// <param name="request">The request data object. Must not be null.</param>
        /// <param name="completionOption">When the operation should complete (as soon as a response is available or after reading the whole response content).</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that returns a <see cref="Result"/> representing the outcome of the request.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="httpClient"/>, <paramref name="method"/>, <paramref name="endpoint"/>, or <paramref name="request"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="endpoint"/> is empty or consists only of whitespace.</exception>
        public Task<Result> SendRequestAsync(HttpMethod method,
            [StringSyntax("Uri")] string endpoint,
            IRequest request,
            HttpCompletionOption completionOption = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
            return httpClient.SendRequestAsync(method, new Uri(endpoint, UriKind.RelativeOrAbsolute), request, completionOption, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP request and expects a typed response.
        /// </summary>
        /// <typeparam name="TResponse">The expected response type.</typeparam>
        /// <param name="method">The HTTP method.</param>
        /// <param name="endpoint">The URI endpoint.</param>
        /// <param name="request">The request object implementing <see cref="IRequest{TResponse}"/>.</param>
        /// <param name="completionOption">Completion option.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that returns a <see cref="Result{TResponse}"/>.</returns>
        public Task<Result<TResponse>> SendRequestAsync<TResponse>(HttpMethod method,
            [StringSyntax("Uri")] string endpoint,
            IRequest<TResponse> request,
            HttpCompletionOption completionOption = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
            return httpClient.SendRequestAsync(method, new Uri(endpoint, UriKind.RelativeOrAbsolute), request, completionOption, cancellationToken);
        }

        /// <summary>
        /// Internal implementation for sending a request with a non-generic response.
        /// </summary>
        public async Task<Result> SendRequestAsync(HttpMethod method,
            Uri uri,
            IRequest request,
            HttpCompletionOption completionOption = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            using var response = await httpClient.GetResponseAsync(method, uri, request, completionOption, cancellationToken);
            return await ToResultAsync(response, cancellationToken);

            static async Task<Result> ToResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
            {
                if (!response.IsSuccessStatusCode)
                {
                    // Return failure result with status code and reason
                    return Result.Fail((int)response.StatusCode, $"{(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                try
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<Result>(Options, cancellationToken: cancellationToken);
                    // Defensive check: although unlikely, ensure deserialized result is not null
                    return apiResponse ?? Result.Fail(500, "Unexpected null response from server.");
                }
                catch (JsonException ex)
                {
                    // Deserialization failed → client sent malformed data or server returned invalid JSON
                    return Result.Fail(400, $"Response deserialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Internal implementation for sending a request with a generic response.
        /// </summary>
        public async Task<Result<TResponse>> SendRequestAsync<TResponse>(HttpMethod method,
            Uri uri,
            IRequest<TResponse> request,
            HttpCompletionOption completionOption = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            var response = await httpClient.GetResponseAsync(method, uri, request, completionOption, cancellationToken);
            return await ToResultAsync<TResponse>(response, cancellationToken);

            static async Task<Result<T>> ToResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return Result.Fail((int)response.StatusCode, $"{(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                try
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<Result<T>>(Options, cancellationToken: cancellationToken);
                    return apiResponse ?? Result.Fail(500, "Unexpected null response from server.");
                }
                catch (JsonException ex)
                {
                    return Result.Fail(400, $"Response deserialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Constructs and sends the actual HTTP request message.
        /// Serializes the request body or appends query parameters based on the HTTP method.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="uri">Target URI.</param>
        /// <param name="request">Request data object.</param>
        /// <param name="completionOption">Completion option.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The HTTP response message.</returns>
        internal async Task<HttpResponseMessage> GetResponseAsync(HttpMethod method,
            Uri uri,
            IRequestBase request,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(request);

            HttpResponseMessage? response = null;
            try
            {
                if (ShouldUseBody(method))
                {
                    using var httpRequest = new HttpRequestMessage(method, uri);
                    httpRequest.Content = JsonContent.Create(request, request.GetType(), options: Options);
                    response = await httpClient.SendAsync(httpRequest, completionOption, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var originalUriString = uri.ToString();
                    var queryIndex = originalUriString.IndexOf('?');
                    string pathAndFragment;
                    string existingQuery;

                    if (queryIndex >= 0)
                    {
                        pathAndFragment = originalUriString[..queryIndex];
                        existingQuery = originalUriString[(queryIndex + 1)..];
                    }
                    else
                    {
                        pathAndFragment = originalUriString;
                        existingQuery = string.Empty;
                    }

                    var queryParams = FlattenToQueryParams(request);
                    var nonNullParams = queryParams.Where(kvp => !string.IsNullOrEmpty(kvp.Value)).ToList();

                    var queryParts = new List<string>();

                    if (!string.IsNullOrEmpty(existingQuery))
                    {
                        queryParts.Add(existingQuery);
                    }

                    if (nonNullParams.Count > 0)
                    {
                        var newQuery = string.Join("&", nonNullParams.Select(kvp =>
                        {
                            var escapedKey = Uri.EscapeDataString(kvp.Key);
                            var escapedValue = Uri.EscapeDataString(kvp.Value!);
                            return $"{escapedKey}={escapedValue}";
                        }));
                        queryParts.Add(newQuery);
                    }

                    var finalQuery = string.Join("&", queryParts);
                    var finalUriString = string.IsNullOrEmpty(finalQuery)
                        ? pathAndFragment
                        : $"{pathAndFragment}?{finalQuery}";

                    var newUri = uri.IsAbsoluteUri
                        ? new Uri(finalUriString, UriKind.Absolute)
                        : new Uri(finalUriString, UriKind.Relative);

                    using var httpRequest = new HttpRequestMessage(method, newUri);
                    response = await httpClient.SendAsync(httpRequest, completionOption, cancellationToken).ConfigureAwait(false);
                }

                return response;
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// Flattens an object into a dictionary of property names and string representations for use in query strings.
    /// Handles common types like DateTime, DateOnly, etc., with appropriate formatting.
    /// </summary>
    /// <param name="obj">The object to flatten. Must not be null.</param>
    /// <returns>A dictionary mapping property names to their string values (or null if the property value is null).</returns>
    private static Dictionary<string, string?> FlattenToQueryParams(object obj)
    {
        var type = obj.GetType();
        var properties = PropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray());

        var result = new Dictionary<string, string?>(properties.Length);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            if (value is null)
            {
                result[prop.Name] = null;
                continue;
            }

            var stringValue = value switch
            {
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly t => t.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
                _ => value.ToString()
            };

            result[prop.Name] = stringValue;
        }

        return result;
    }

    /// <summary>
    /// Determines whether the given HTTP method should carry a request body.
    /// Only POST, PUT, and PATCH are considered body-capable for semantic correctness.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns><c>true</c> if the method should use a body; otherwise, <c>false</c>.</returns>
    internal static bool ShouldUseBody(HttpMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return method == HttpMethod.Post ||
               method == HttpMethod.Put ||
               method == HttpMethod.Patch;
    }
}