using System;
using System.Collections.Specialized;
using System.Net;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Transport.Http.Codecs;
using System.Linq;

namespace EventStore.Transport.Http.EntityManagement
{
    public class HttpEntity
    {
        private readonly bool _logHttpRequests;
        public readonly Uri RequestedUrl;

        public readonly HttpListenerRequest Request;
        internal readonly HttpListenerResponse Response;
        public readonly IPrincipal User;

        public HttpEntity(HttpListenerRequest request, HttpListenerResponse response, IPrincipal user, bool logHttpRequests, IPEndPoint externalHttpEndPoint)
        {
            Ensure.NotNull(request, "request");
            Ensure.NotNull(response, "response");

            _logHttpRequests = logHttpRequests;
            RequestedUrl = BuildRequestedUrl(request.Url, request.Headers, externalHttpEndPoint);
            Request = request;
            Response = response;
            User = user;
        }

        public static Uri BuildRequestedUrl(Uri requestUrl, NameValueCollection requestHeaders, IPEndPoint externalHttpEndPoint)
        {
            var uriBuilder = new UriBuilder(requestUrl);

            if(externalHttpEndPoint != null)
            {
                uriBuilder.Host = externalHttpEndPoint.Address.ToString();
                uriBuilder.Port = externalHttpEndPoint.Port;
            }
            
            var forwardedPortHeaderValue = requestHeaders[ProxyHeaders.XForwardedPort];
            if (!string.IsNullOrEmpty(forwardedPortHeaderValue))
            {
                int requestPort;
                if (Int32.TryParse(forwardedPortHeaderValue, out requestPort))
                {
                    uriBuilder.Port = requestPort;
                }
            }

            var forwardedProtoHeaderValue = requestHeaders[ProxyHeaders.XForwardedProto];
            if (!string.IsNullOrEmpty(forwardedProtoHeaderValue))
            {
                uriBuilder.Scheme = forwardedProtoHeaderValue;
            }

            var forwardedHostHeaderValue = requestHeaders[ProxyHeaders.XForwardedHost];
            if (!string.IsNullOrEmpty(forwardedHostHeaderValue))
            {
                var host = forwardedHostHeaderValue.Split(new []{","}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if(!string.IsNullOrEmpty(host)) 
                {
                    var parts = host.Split(new []{":"}, StringSplitOptions.RemoveEmptyEntries);
                    uriBuilder.Host = parts.First();
                    int port;
                    if(parts.Count() > 1 && int.TryParse(parts[1], out port)) {
                        uriBuilder.Port = port;
                    }
                }
            }

            return uriBuilder.Uri;
        }

        private HttpEntity(IPrincipal user, bool logHttpRequests)
        {
            RequestedUrl = null;

            Request = null;
            Response = null;
            User = user;
            _logHttpRequests = logHttpRequests;
        }

        private HttpEntity(HttpEntity httpEntity, IPrincipal user, bool logHttpRequests)
        {
            RequestedUrl = httpEntity.RequestedUrl;

            Request = httpEntity.Request;
            Response = httpEntity.Response;
            User = user;
            _logHttpRequests = logHttpRequests;
        }

        public HttpEntityManager CreateManager(
            ICodec requestCodec, ICodec responseCodec, string[] allowedMethods, Action<HttpEntity> onRequestSatisfied)
        {
            return new HttpEntityManager(this, allowedMethods, onRequestSatisfied, requestCodec, responseCodec, _logHttpRequests);
        }

        public HttpEntityManager CreateManager()
        {
            return new HttpEntityManager(this, Empty.StringArray, entity => { }, Codec.NoCodec, Codec.NoCodec, _logHttpRequests);
        }

        public HttpEntity SetUser(IPrincipal user)
        {
            return new HttpEntity(this, user, _logHttpRequests);
        }

        public static HttpEntity Test(IPrincipal user)
        {
            return new HttpEntity(user, false);
        }
    }
}