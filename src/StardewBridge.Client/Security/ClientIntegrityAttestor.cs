using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using StardewHeadless.Protocol.Packets;

namespace StardewBridge.Client.Security;

/// <summary>
/// Komponen Lapis 1 (Client Attestation & Integrity Guard) pada StardewBridge.Client.
/// Melakukan audit integritas binary DLL mod, memindai tabel patch Harmony pada metode
/// sensitif game, dan menghasilkan security heartbeat berkala bertanda tangan HMAC.
/// </summary>
public static class ClientIntegrityAttestor
{
    private static readonly HashSet<string> DefaultAllowedOwners = new(StringComparer.OrdinalIgnoreCase)
    {
        "IJP.StardewBridge",
        "stardew.headless",
        "Pathoschild.LookupAnything",
        "Pathoschild.ContentPatcher"
    };

    private static readonly HashSet<string> SensitiveTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BobberBar",
        "FishingRod",
        "Farmer",
        "GameLocation"
    };

    private static readonly HashSet<string> SensitiveMethodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pullFishFromWater",
        "doDoneFishing",
        "tickUpdate",
        "getMovementSpeed",
        "takeDamage",
        "MovePosition",
        "set_Money",
        "set_Stamina"
    };

    /// <summary>
    /// Menghitung hash SHA-256 dari file binary DLL mod.
    /// Mencegah eksploitasi penggantian nama UniqueID di manifest.json.
    /// </summary>
    public static string ComputeFileSha256(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Memindai tabel patch Harmony runtime menggunakan Harmony.GetAllPatchedMethods().
    /// Mendeteksi injeksi mod asing yang memodifikasi mekanik memancing, kecepatan, HP, atau uang.
    /// </summary>
    public static List<HarmonyPatchAnomaly> ScanSensitiveHarmonyPatches(IEnumerable<string>? whitelistedOwners = null)
    {
        var anomalies = new List<HarmonyPatchAnomaly>();
        var allowed = new HashSet<string>(DefaultAllowedOwners, StringComparer.OrdinalIgnoreCase);
        if (whitelistedOwners != null)
        {
            foreach (var w in whitelistedOwners)
            {
                allowed.Add(w);
            }
        }

        try
        {
            var patchedMethods = Harmony.GetAllPatchedMethods().ToList();

            foreach (var method in patchedMethods)
            {
                if (method == null) continue;

                string typeName = method.DeclaringType?.Name ?? string.Empty;
                string methodName = method.Name;

                bool isSensitive = SensitiveTypeNames.Contains(typeName) || SensitiveMethodNames.Contains(methodName);
                if (!isSensitive) continue;

                var patchInfo = Harmony.GetPatchInfo(method);
                if (patchInfo == null) continue;

                var allOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (patchInfo.Owners != null)
                {
                    foreach (var o in patchInfo.Owners)
                    {
                        if (!string.IsNullOrWhiteSpace(o)) allOwners.Add(o);
                    }
                }

                foreach (var owner in allOwners)
                {
                    if (!allowed.Contains(owner))
                    {
                        anomalies.Add(new HarmonyPatchAnomaly
                        {
                            MethodName = $"{typeName}.{methodName}",
                            PatchOwnerId = owner,
                            PatchAssemblyName = method.DeclaringType?.Assembly?.GetName().Name ?? "Unknown"
                        });
                    }
                }
            }
        }
        catch
        {
            // Fallback graceful jika lingkungan tanpa Harmony runtime
        }

        return anomalies;
    }

    /// <summary>
    /// Menghasilkan tanda tangan digital HMAC-SHA256 untuk paket heartbeat integritas 0x9C.
    /// </summary>
    public static string GenerateHeartbeatSignature(long farmerId, long timestampTicks, string nonce, string secretKey)
    {
        string payload = $"{farmerId}:{timestampTicks}:{nonce}";
        byte[] keyBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(secretKey) ? "StardewBridgeDefaultKey" : secretKey);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Memverifikasi tanda tangan digital HMAC-SHA256 dari paket heartbeat di sisi server.
    /// </summary>
    public static bool VerifyHeartbeatSignature(long farmerId, long timestampTicks, string nonce, string signature, string secretKey)
    {
        string expected = GenerateHeartbeatSignature(farmerId, timestampTicks, nonce, secretKey);
        return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
    }
}
