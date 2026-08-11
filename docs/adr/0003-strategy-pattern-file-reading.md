# ADR 0003: Strategy Pattern for File Reading

## CsvHelper over manual parsing

The job search CSV contains multi-line quoted fields in the Notes column. A hand-rolled `ReadLine()` parser breaks on these. CsvHelper handles RFC 4180 compliant CSV correctly out of the box.

## Strategy Pattern (ICsvReaderService)

`ICsvReaderService` defines the contract for reading structured data files. `CsvReaderService` and `JsonReaderService` are interchangeable implementations. Swapping one for the other requires changing a single line in `Program.cs` - the controller, job processor, and all callers remain untouched. This is the Strategy Pattern: same interface, swappable behavior, caller never knows the difference. A proof test in `CsvReaderServiceTests` verifies both implementations return identical results from the same data in different formats.
