using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Repl.Server.Core.GrpcInterceptors;

public class TimeoutInterceptor : Interceptor
    {
        private readonly TimeSpan timeout;

        public TimeoutInterceptor(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        private void ApplyTimeout<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
            where TRequest : class
            where TResponse : class
        {
            if (context.Options.Deadline is null)
            {
                context = new(context.Method, context.Host, context.Options.WithDeadline(DateTime.UtcNow.Add(timeout)));
            }
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            this.ApplyTimeout(ref context);
            return base.AsyncClientStreamingCall(context, continuation);
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            this.ApplyTimeout(ref context);
            return base.AsyncDuplexStreamingCall(context, continuation);
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            this.ApplyTimeout(ref context);
            return base.AsyncServerStreamingCall(request, context, continuation);
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            this.ApplyTimeout(ref context);
            return base.AsyncUnaryCall(request, context, continuation);
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            this.ApplyTimeout(ref context);
            return base.BlockingUnaryCall(request, context, continuation);
        }
    }