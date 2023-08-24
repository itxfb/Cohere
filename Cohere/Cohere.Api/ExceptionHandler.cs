using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Cohere.Api
{
    /// <summary>
    /// Middleware - error handling
    /// </summary>
    public class ExceptionHandler
    {
        private readonly RequestDelegate _next;

        public ExceptionHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next.Invoke(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
                if (Startup.Configuration["Exception:ThrowExceptionAfterLog"] == "True")
                {
                    throw ex;
                }
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";
            response.StatusCode = (int)HttpStatusCode.InternalServerError;

            //get inner if exists
            var innerExceptionMsg = string.Empty;
            if (exception.InnerException != null)
            {
                innerExceptionMsg = exception.InnerException.Message;
            }

            var errorClass = _next.Target.GetType();
            var errorMethod = _next.Method;

            var errorSerialized = JsonSerializer.Serialize(new
            {
                Error = new
                {
                    Message = $"Class Name: {errorClass} - Method Name: {errorMethod} - OuterException: {exception.Message} {Environment.NewLine} InnerException: {innerExceptionMsg}",
                    ExceptionName = exception.GetType().Name
                }
            });

            await response.WriteAsync(errorSerialized);

            //serilog
            Log.Error($"ERROR FOUND: {errorSerialized}                  Stack trace: {exception.StackTrace}");
        }
    }
}