using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using System.Net;

namespace AeroServer
{
    // Resolves all DNS queries to target IP address.
    public class DnsRequestResolver : IRequestResolver
    {
        private readonly IPAddress _targetIp;

        public DnsRequestResolver(IPAddress targetIp)
        {
            _targetIp = targetIp;
        }

        public Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken)
        {
            IResponse response = Response.FromRequest(request);

            foreach (Question question in response.Questions)
            {
                if (question.Type == RecordType.A)
                {
                    IResourceRecord record = new IPAddressResourceRecord(
                        question.Name, _targetIp
                    );

                    response.AnswerRecords.Add(record);
                }
            }

            return Task.FromResult(response);
        }
    }
}