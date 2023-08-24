using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Cohere.Domain.Utils
{
	/// <summary>
	/// Helper class for retrying method calls.
	/// </summary>
	public static class RetryHelper
	{
		/// <summary>
		/// Retry wrapper for a method.
		/// </summary>
		/// <param name="retryCount">The number of times to try the method.</param>
		/// <param name="delayBetweenRetries">How long to pause between retries.</param>
		/// <param name="cancellationToken">Token for cancelling the method.</param>
		/// <param name="operation">The method to execute.</param>
		/// <returns>True if the method was successfully executed.</returns>
		public static async Task<TReturnType> RetryOnExceptionAsync<TReturnType>(int retryCount, TimeSpan delayBetweenRetries,
			CancellationToken cancellationToken, Func<Task<TReturnType>> operation, ILogger logger = null)
		{
			return await RetryHelper.RetryOnExceptionAsync<Exception, TReturnType>(retryCount, delayBetweenRetries,
				cancellationToken, operation, null, logger);
		}

		/// <summary>
		/// Retry wrapper for a method with an expected exception type to handle.
		/// </summary>
		/// <param name="retryCount">The number of times to try the method.</param>
		/// <param name="delayBetweenRetries">How long to pause between retries.</param>
		/// <param name="cancellationToken">Token for cancelling the method.</param>
		/// <param name="operation">The method to execute.</param>
		/// <returns>True if the method was successfully executed.</returns>
		public static async Task<TReturnType> RetryOnExceptionAsync<TException, TReturnType>(int retryCount, TimeSpan delayBetweenRetries,
			CancellationToken cancellationToken, Func<Task<TReturnType>> operation, ILogger logger) where TException : Exception
		{
			return await RetryHelper.RetryOnExceptionAsync<TException, TReturnType>(retryCount, delayBetweenRetries,
				cancellationToken, operation, null, logger);
		}

		/// <summary>
		/// Retry wrapper for a method.
		/// </summary>
		/// <param name="retryCount">The number of times to try the method.</param>
		/// <param name="delayBetweenRetries">How long to pause between retries.</param>
		/// <param name="cancellationToken">Token for cancelling the method.</param>
		/// <param name="operation">The method to execute.</param>
		/// <param name="exceptionHandler">The method to execute for handling a possible exception that occurred.</param>
		/// <returns>True if the method was successfully executed.</returns>
		public static async Task<TReturnType> RetryOnExceptionAsync<TReturnType>(int retryCount, TimeSpan delayBetweenRetries,
			CancellationToken cancellationToken, Func<Task<TReturnType>> operation, Func<Exception, Task> exceptionHandler, ILogger logger)
		{
			return await RetryHelper.RetryOnExceptionAsync<Exception, TReturnType>(retryCount, delayBetweenRetries,
				cancellationToken, operation, exceptionHandler, logger);
		}

		/// <summary>
		/// Retry wrapper for a method with an expected exception type to handle.
		/// </summary>
		/// <param name="retryCount">The number of times to try the method.</param>
		/// <param name="delayBetweenRetries">How long to pause between retries.</param>
		/// <param name="cancellationToken">Token for cancelling the method.</param>
		/// <param name="operation">The method to execute.</param>
		/// <param name="exceptionHandler">The method to execute for handling a possible exception that occurred.</param>
		/// <returns>True if the method was successfully executed.</returns>
		public static async Task<TReturnType> RetryOnExceptionAsync<TException, TReturnType>(int retryCount, TimeSpan delayBetweenRetries,
			CancellationToken cancellationToken, Func<Task<TReturnType>> operation, Func<Exception, Task> exceptionHandler,
			ILogger logger) where TException : Exception
		{
			TReturnType result = default(TReturnType);
			// Argument validation
			if (retryCount <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(retryCount));
			}
			if (operation == null)
			{
				throw new ArgumentNullException(nameof(operation));
			}

			//bool successfullyExecuted = false;

			// Start retrying the method
			int attempts = 0;
			do
			{
				try
				{
					attempts++;
					if(logger != null)
					{
						logger.LogInformation($"RetryHelp - attemp {attempts} for operation {operation?.Method?.Name}");
					}
					result = await operation();
					//successfullyExecuted = true;
					break;
				}
				catch (TException e)
				{
					if (attempts == retryCount)
					{
						if (logger != null)
						{
							logger.LogInformation($"RetryHelp - too many attempts ... throwing");
						}
						throw;
					}
					if (exceptionHandler != null)
					{
						try
						{
							await exceptionHandler(e);
						}
						catch { }
					}
					if (logger != null)
					{
						logger.LogInformation($"Operation {operation?.Method?.Name} failed .. Retrying ..");
					}
					await Task.Delay(delayBetweenRetries, cancellationToken);
				}
			} while (true && !cancellationToken.IsCancellationRequested);

			//return successfullyExecuted;
			if (logger != null)
			{
				string nullResult = result == null ? "(null)" : "";
				logger.LogInformation($"RetryHelp - returning result {nullResult}");
			}
			return result;
		}
	}
}
