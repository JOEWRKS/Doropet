using Xunit;

// These tests create WPF presenters on short-lived STA threads. WPF's shared
// pack-resource cache is process-wide and PackagePart stream tracking is not
// safe under concurrent LoadComponent calls. Match the product's single UI
// thread instead of racing unrelated test classes through the same BAML cache.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
