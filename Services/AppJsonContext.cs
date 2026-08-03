using System.Text.Json.Serialization;
using MGA_RegistryChecker.Models;
using Microsoft.Win32;

namespace MGA_RegistryChecker.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(WatchedLocation))]
[JsonSerializable(typeof(RegistryKeySnapshot))]
[JsonSerializable(typeof(RegistryValueData))]
[JsonSerializable(typeof(RegistryValueKind))]
internal partial class AppJsonContext : JsonSerializerContext;
