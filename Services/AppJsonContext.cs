using System.Text.Json.Serialization;
using MgaRegistryChecker.Models;
using Microsoft.Win32;

namespace MgaRegistryChecker.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(WatchedLocation))]
[JsonSerializable(typeof(RegistryKeySnapshot))]
[JsonSerializable(typeof(RegistryValueData))]
[JsonSerializable(typeof(RegistryValueKind))]
[JsonSerializable(typeof(WindowBounds))]
internal partial class AppJsonContext : JsonSerializerContext;
