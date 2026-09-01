using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class ApplicationIntegrityResult
    {
        private ApplicationIntegrityResult(bool isValid, string failureReason)
        {
            IsValid = isValid;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool IsValid { get; }
        public string FailureReason { get; }

        public static ApplicationIntegrityResult Valid()
        {
            return new ApplicationIntegrityResult(true, string.Empty);
        }

        public static ApplicationIntegrityResult Invalid(string reason)
        {
            return new ApplicationIntegrityResult(false, reason);
        }
    }

    internal static class ApplicationIntegrityVerifier
    {
        private const string CertificateResourceName = "DismToolGui.Security.DISM.cer";
        private const int TrustSuccess = 0;
        private const int CertificateUntrustedRoot = unchecked((int)0x800B0109);
        private const int CertificateChaining = unchecked((int)0x800B010A);

        public static ApplicationIntegrityResult VerifyCurrentExecutable()
        {
            return VerifyExecutable(Application.ExecutablePath);
        }

        internal static ApplicationIntegrityResult VerifyExecutable(string executablePath)
        {
            try
            {
                int trustStatus =
                    AuthenticodeTrust.GetEmbeddedSignatureVerificationStatus(executablePath);
                if (!HasIntactPinnedSignature(trustStatus))
                {
                    return ApplicationIntegrityResult.Invalid(
                        $"Authenticode verification failed (0x{trustStatus:X8}).");
                }

                using X509Certificate2 expectedCertificate = LoadExpectedCertificate();
                using X509Certificate signerCertificate =
                    X509Certificate.CreateFromSignedFile(executablePath);
                using X509Certificate2 actualCertificate =
                    new X509Certificate2(signerCertificate);
                if (!FixedTimeEquals(expectedCertificate.RawData, actualCertificate.RawData))
                {
                    return ApplicationIntegrityResult.Invalid(
                        "The executable was signed by an unexpected certificate.");
                }

                return ApplicationIntegrityResult.Valid();
            }
            catch (Exception ex) when (
                ex is CryptographicException ||
                ex is FormatException ||
                ex is IOException ||
                ex is InvalidOperationException ||
                ex is UnauthorizedAccessException)
            {
                return ApplicationIntegrityResult.Invalid(
                    $"Signature verification could not be completed: {ex.Message}");
            }
        }

        private static bool HasIntactPinnedSignature(int trustStatus)
        {
            // The supplied certificate chains through Codegic, whose root may not
            // be installed on every target PC. The exact embedded certificate is
            // the application trust anchor, while WinVerifyTrust still rejects an
            // absent signature, a bad digest, or a modified executable.
            return trustStatus == TrustSuccess ||
                   trustStatus == CertificateUntrustedRoot ||
                   trustStatus == CertificateChaining;
        }

        private static X509Certificate2 LoadExpectedCertificate()
        {
            Assembly assembly = typeof(ApplicationIntegrityVerifier).Assembly;
            using Stream stream = assembly.GetManifestResourceStream(CertificateResourceName);
            if (stream == null)
                throw new InvalidOperationException("The official signing certificate is missing.");

            using var reader = new StreamReader(stream);
            string pem = reader.ReadToEnd();
            string base64 = pem
                .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
                .Replace("-----END CERTIFICATE-----", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
            return new X509Certificate2(Convert.FromBase64String(base64));
        }

        private static bool FixedTimeEquals(byte[] expected, byte[] actual)
        {
            if (expected == null || actual == null)
                return false;

            int difference = expected.Length ^ actual.Length;
            int length = Math.Min(expected.Length, actual.Length);
            for (int index = 0; index < length; index++)
                difference |= expected[index] ^ actual[index];

            return difference == 0;
        }
    }
}
