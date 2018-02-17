﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Kayak.Http;
using Kayak;


namespace IW4MAdmin
{
    class Scheduler : ISchedulerDelegate
    {
        public void OnException(IScheduler scheduler, Exception e)
        {
            // it looks like there's a library error in
            // Kayak.Http.HttpServerTransactionDelegate.OnError
            if ((uint)e.HResult == 0x80004003 || (uint)e.InnerException?.HResult == 0x80004003)
                return;

            ApplicationManager.GetInstance().Logger.WriteWarning("Web service has encountered an error - " + e.Message);
            ApplicationManager.GetInstance().Logger.WriteDebug($"Stack Trace: {e.StackTrace}");

            if (e.InnerException != null)
            {
                ApplicationManager.GetInstance().Logger.WriteDebug($"Inner Exception: {e.InnerException.Message}");
                ApplicationManager.GetInstance().Logger.WriteDebug($"Inner Stack Trace: {e.InnerException.StackTrace}");
            }

        }

        public void OnStop(IScheduler scheduler)
        {
            ApplicationManager.GetInstance().Logger.WriteInfo("Web service has been stopped...");
        }
    }

    class Request : IHttpRequestDelegate
    {
        public void OnRequest(HttpRequestHead request, IDataProducer requestBody, IHttpResponseDelegate response)
        {
#if DEBUG
            var logger = ApplicationManager.GetInstance().GetLogger();
            logger.WriteDebug($"HTTP request {request.Path}");
            logger.WriteDebug($"QueryString: {request.QueryString}");
            logger.WriteDebug($"IP: {request.IPAddress}");
#endif

            NameValueCollection querySet = new NameValueCollection();

            if (request.QueryString != null)
                querySet = System.Web.HttpUtility.ParseQueryString(request.QueryString);

            querySet.Set("IP", request.IPAddress);

            try
            {
                request.Path = String.IsNullOrEmpty(request.Path) ? "/" : request.Path;
                SharedLibrary.HttpResponse requestedPage = WebService.GetPage(request.Path, querySet, request.Headers);

                bool binaryContent = requestedPage.BinaryContent != null;
                if (requestedPage.content != null && requestedPage.content.GetType() != typeof(string))
#if !DEBUG
                    requestedPage.content = Newtonsoft.Json.JsonConvert.SerializeObject(requestedPage.content);
#else
                    requestedPage.content = Newtonsoft.Json.JsonConvert.SerializeObject(requestedPage.content, Newtonsoft.Json.Formatting.Indented);
#endif

                string maxAge = requestedPage.contentType == "application/json" ? "0" : "21600";
                var headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", requestedPage.contentType },
                        { "Content-Length", binaryContent ? requestedPage.BinaryContent.Length.ToString() : requestedPage.content.ToString().Length.ToString() },
                        { "Access-Control-Allow-Origin", "*" },
                        { "Cache-Control", $"public,max-age={maxAge}"}
                    }
                };

                foreach (var key in requestedPage.additionalHeaders.Keys)
                    headers.Headers.Add(key, requestedPage.additionalHeaders[key]);
                if (!binaryContent)
                    response.OnResponse(headers, new BufferedProducer((string)requestedPage.content));
                else
                    response.OnResponse(headers, new BufferedProducer(requestedPage.BinaryContent));
            }

            catch (Exception e)
            {
                if (e.GetType() == typeof(FormatException))
                {
                    ApplicationManager.GetInstance().Logger.WriteWarning("Request parameter data format was incorrect");
                    ApplicationManager.GetInstance().Logger.WriteDebug($"Request Path {request.Path}");
                    ApplicationManager.GetInstance().Logger.WriteDebug($"Request Query String {request.QueryString}");
                    response.OnResponse(new HttpResponseHead()
                    {
                        Status = "400 Bad Request",
                        Headers = new Dictionary<string, string>()
                        {
                            { "Content-Type", "text/html" },
                            { "Content-Length", "0"},
                        }
                    }, new BufferedProducer(""));
                }

                else
                {
                    ApplicationManager.GetInstance().Logger.WriteError($"Webfront error during request");
                    ApplicationManager.GetInstance().Logger.WriteDebug($"Message: {e.Message}");
                    ApplicationManager.GetInstance().Logger.WriteDebug($"Stack Trace: {e.StackTrace}");

                    response.OnResponse(new HttpResponseHead()
                    {
                        Status = "500 Internal Server Error",
                        Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/html" },
                        { "Content-Length", "0"},
                    }
                    }, new BufferedProducer(""));
                }
            }
        }
    }

    class BufferedProducer : IDataProducer
    {
        ArraySegment<byte> data;

        public BufferedProducer(string data) : this(data, Encoding.ASCII) { }
        public BufferedProducer(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }
        public BufferedProducer(byte[] data) : this(new ArraySegment<byte>(data)) { }

        public BufferedProducer(ArraySegment<byte> data)
        {
            this.data = data;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            try
            {
                channel?.OnData(data, null);
                channel?.OnEnd();
            }

            catch (Exception)
            {

            }

            return null;
        }
    }

    class BufferedConsumer : IDataConsumer
    {
        List<ArraySegment<byte>> buffer = new List<ArraySegment<byte>>();
        Action<string> resultCallback;
        Action<Exception> errorCallback;

        public BufferedConsumer(Action<string> resultCallback, Action<Exception> errorCallback)
        {
            this.resultCallback = resultCallback;
            this.errorCallback = errorCallback;
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            // this should hopefully clean the non ascii characters out.
            buffer?.Add(new ArraySegment<byte>(Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(data.ToArray()))));
            return false;
        }

        public void OnError(Exception error)
        {
            //  errorCallback?.Invoke(error);
        }

        public void OnEnd()
        {
            var str = buffer
                .Select(b => Encoding.ASCII.GetString(b.Array, b.Offset, b.Count))
                .Aggregate((result, next) => result + next);

            resultCallback(str);
        }
    }
}