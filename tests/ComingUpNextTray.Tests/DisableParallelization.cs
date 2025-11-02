using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Disables parallel test execution because tests mutate the COMINGUPNEXT_TEST_CONFIG_PATH
// environment variable to isolate config paths. Parallel runs could race on this shared global.
