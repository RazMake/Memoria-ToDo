using Grpc.Core;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

[Unit]
public class CallOutcomeStatusTests
{
    [Fact]
    public void An_expected_status_does_not_fail_the_test()
    {
        var failure = new RpcException(new Status(StatusCode.Unavailable, "The app stopped."));

        CallOutcome.AssertStatus(failure, StatusCode.Unavailable, new SignalLogSink());
    }

    [Fact]
    public void A_wrong_status_reports_the_RPC_detail_and_its_nested_transport_failure()
    {
        var transport = new HttpRequestException("The response ended.", new IOException("The pipe closed."));
        var failure = new RpcException(new Status(StatusCode.Internal, "The post-stop call failed.", transport));

        var report = Assert.ThrowsAny<Exception>(() =>
            CallOutcome.AssertStatus(failure, StatusCode.Unavailable, new SignalLogSink()));

        Assert.Contains("Unavailable", report.Message);
        Assert.Contains("Internal", report.Message);
        Assert.Contains("The post-stop call failed.", report.Message);
        Assert.Contains(transport.ToString(), report.Message);
        Assert.Same(failure, report.InnerException);
    }

    [Fact]
    public void A_wrong_status_reports_every_server_entry_with_its_level_properties_and_exception()
    {
        var log = new SignalLogSink();
        var logger = log.CreateLogger().ForContext("PipeName", "test-pipe");
        logger.Information("Listening for calls.");
        logger.Warning("Stopping with {RunningCalls} calls.", 2);
        var stopFailure = new IOException("Closing failed.", new InvalidOperationException("Handle still open."));
        logger.Error(stopFailure, "Stop failed.");
        var failure = new RpcException(new Status(StatusCode.Internal, "The call failed."));

        var report = Assert.ThrowsAny<Exception>(() =>
            CallOutcome.AssertStatus(failure, StatusCode.Unavailable, log));

        Assert.Contains("Information", report.Message);
        Assert.Contains("Listening for calls.", report.Message);
        Assert.Contains("Warning", report.Message);
        Assert.Contains("Stopping with 2 calls.", report.Message);
        Assert.Contains("RunningCalls=2", report.Message);
        Assert.Contains("PipeName=", report.Message);
        Assert.Contains("test-pipe", report.Message);
        Assert.Contains("Error", report.Message);
        Assert.Contains("Stop failed.", report.Message);
        Assert.Contains(stopFailure.ToString(), report.Message);
    }

    [Fact]
    public void A_wrong_status_without_transport_details_or_server_entries_still_reports_the_RPC_failure()
    {
        var failure = new RpcException(new Status(StatusCode.Internal, "No transport details were supplied."));

        var report = Assert.ThrowsAny<Exception>(() =>
            CallOutcome.AssertStatus(failure, StatusCode.Unavailable, new SignalLogSink()));

        Assert.Contains("Unavailable", report.Message);
        Assert.Contains("Internal", report.Message);
        Assert.Contains("No transport details were supplied.", report.Message);
        Assert.Same(failure, report.InnerException);
    }
}
