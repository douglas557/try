﻿using System;
using System.Net.Http;

namespace WorkspaceServer.Models.Execution
{
    public static class HttpRequestExtensions
    {
        public static HttpRequestMessage ToHttpRequestMessage(this HttpRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new HttpRequestMessage(
                new HttpMethod(request.Verb),
                request.Uri)
            {
                Content = new StringContent(request.Body)
            };
        }
    }
}