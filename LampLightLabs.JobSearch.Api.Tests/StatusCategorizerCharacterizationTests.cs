using LampLightLabs.JobSearch.Api.Services;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Characterization tests for StatusCategorizerService.
///
/// PURPOSE: These tests do NOT assert what the categorizer should do.
/// They assert what it actually does today — freezing the current behavior
/// so that any future refactoring that changes the output is caught immediately.
///
/// Following Michael Feathers' four-step approach from "Working Effectively with Legacy Code":
///   1. Write a test that calls the method with a known input.
///   2. Let it run and observe the actual output.
///   3. Update the assertion to match that actual output.
///   4. The test now passes — behavior is frozen.
///
/// NOTE: One test below (Applied_ReturnsUnknown) surfaces a genuine gap in the
/// current logic. "Applied" is a valid real-world status but returns "Unknown"
/// because the categorizer has no rule for it. The characterization test freezes
/// this behavior as-is. A separate fix can be made after the safety net is in place.
/// </summary>
public class StatusCategorizerCharacterizationTests
{
    private readonly IStatusCategorizerService _sut = new StatusCategorizerService();

    // --- Closed category ---

    [Fact]
    public void Closed_ReturnsClosed()
    {
        Assert.Equal("Closed", _sut.Categorize("Closed"));
    }

    [Fact]
    public void Declined_ReturnsClosed()
    {
        Assert.Equal("Closed", _sut.Categorize("Declined"));
    }

    [Fact]
    public void Rejected_ReturnsClosed()
    {
        Assert.Equal("Closed", _sut.Categorize("Rejected"));
    }

    [Fact]
    public void MixedCase_Closed_ReturnsClosed()
    {
        // Verifies the categorizer is case-insensitive.
        Assert.Equal("Closed", _sut.Categorize("CLOSED"));
    }

    // --- OnHold category ---

    [Fact]
    public void OnHold_ReturnsOnHold()
    {
        Assert.Equal("OnHold", _sut.Categorize("On Hold"));
    }

    [Fact]
    public void Waiting_ReturnsOnHold()
    {
        Assert.Equal("OnHold", _sut.Categorize("Waiting"));
    }

    [Fact]
    public void Pending_ReturnsOnHold()
    {
        Assert.Equal("OnHold", _sut.Categorize("Pending"));
    }

    // --- Active category ---

    [Fact]
    public void Active_ReturnsActive()
    {
        Assert.Equal("Active", _sut.Categorize("Active"));
    }

    [Fact]
    public void Submitted_ReturnsActive()
    {
        Assert.Equal("Active", _sut.Categorize("Submitted"));
    }

    [Fact]
    public void InterviewScheduled_ReturnsActive()
    {
        Assert.Equal("Active", _sut.Categorize("Interview Scheduled"));
    }

    [Fact]
    public void WarmContact_ReturnsActive()
    {
        // "Warm" is a substring match — "Warm Contact" resolves to Active.
        Assert.Equal("Active", _sut.Categorize("Warm Contact"));
    }

    // --- Unknown category — including the gap ---

    [Fact]
    public void EmptyString_ReturnsUnknown()
    {
        Assert.Equal("Unknown", _sut.Categorize(""));
    }

    [Fact]
    public void Applied_ReturnsUnknown()
    {
        // GAP: "Applied" is a valid real-world status but the current categorizer
        // has no rule for it and returns "Unknown". This test freezes that behavior.
        // A future refactor should add an "Applied" -> "Active" rule and update
        // this assertion at that time.
        Assert.Equal("Unknown", _sut.Categorize("Applied"));
    }
}
