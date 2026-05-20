using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Server;
using System.Net;

using static AeroServer.Utility;

namespace AeroServer
{
    public class WildcardResolver : IRequestResolver
    {
        private static readonly string[] EXCLUDED_DNS_DOMAIN_PATTERNS = {
            "playstation.net",
            "playstation.com",
            "sony.com",
            "sonyentertainmentnetwork.com",
            "np.ac.",
            "np.community.",
            "psorg-web",
            "ndmdhs.com",
            "np.dl.playstation.net",
        };

        private readonly IPAddress _redirectIp;
        private readonly MasterFile _masterFile;

        public WildcardResolver(string ipAddress, MasterFile masterFile)
        {
            _redirectIp = IPAddress.Parse(ipAddress);
            _masterFile = masterFile;
        }

        public async Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default)
        {
            foreach (Question question in request.Questions)
            {
                await LogAsync($"[DNS] Query: {question.Type} {question.Name}", ConsoleColor.Yellow);
            }

            // First try the master file (explicit domain mappings)
            IResponse response = await _masterFile.Resolve(request, cancellationToken);

            if (response.AnswerRecords.Count > 0)
            {
                foreach (IResourceRecord record in response.AnswerRecords)
                    await LogAsync($"[DNS] Master file resolved: {record.Name} -> {record}", ConsoleColor.Green);

                return response;
            }

            Response wildcardResponse = Response.FromRequest(request);

            foreach (Question question in request.Questions)
            {
                if (question.Type == RecordType.A)
                {
                    string domainName = question.Name.ToString();

                    // TSS domains must be intercepted before the exclusion check
                    if (domainName.EndsWith(".ww.np.dl.playstation.net", StringComparison.OrdinalIgnoreCase))
                    {
                        wildcardResponse.AnswerRecords.Add(new IPAddressResourceRecord(question.Name, _redirectIp));
                        await LogAsync($"[DNS] TSS redirect: {domainName} -> {_redirectIp}");
                        return wildcardResponse;
                    }

                    // Skip excluded domains - let them resolve via real DNS
                    if (IsExcludedDomain(domainName))
                    {
                        await LogAsync($"[DNS] Skipping excluded domain: {domainName}", ConsoleColor.DarkYellow);
                        continue;
                    }

                    wildcardResponse.AnswerRecords.Add(new IPAddressResourceRecord(question.Name, _redirectIp));
                    await LogAsync($"[DNS] Wildcard redirect: {domainName} -> {_redirectIp}");
                }
            }

            if (wildcardResponse.AnswerRecords.Count == 0)
            {
                IResponse upstreamResponse = await new UdpRequestResolver(IPEndPoint.Parse("8.8.8.8")).Resolve(request, cancellationToken);

                foreach (IResourceRecord record in upstreamResponse.AnswerRecords)
                    await LogAsync($"[DNS] Upstream resolved: {record.Name} -> {record}", ConsoleColor.DarkGray);

                if (upstreamResponse.AnswerRecords.Count == 0)
                    await LogAsync($"[DNS] Upstream returned no records for: {string.Join(", ", request.Questions.Select(q => q.Name))}", ConsoleColor.Red);

                return upstreamResponse;
            }

            return wildcardResponse;
        }

        private static bool IsExcludedDomain(string domain)
        {
            foreach (string pattern in EXCLUDED_DNS_DOMAIN_PATTERNS)
            {
                if (domain.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}