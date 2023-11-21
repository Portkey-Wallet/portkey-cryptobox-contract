using System;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using Xunit;

namespace Portkey.Contracts.RedPacket
{
    public class RedPacketContractTests : RedPacketContractTestBase
    {
        [Fact]
        public async Task CreateRedPacketTest()
        {
            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();

            var message = $"{"ELF"}-{10}-{10}";
            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray).ToHex();
            var id = Guid.NewGuid().ToString();
            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var txResult = await RedPacketContractStub.CreateRedPacket.SendAsync(new CreateRedPacketInput
            {
                RedPacketSymbol  = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                FromSender = DefaultAddress,
                PublicKey = publicKey,
                RedPacketType = RedPacketType.QuickTransfer,
                ExpirationTime = timeSeconds + 1000,
                RedPacketSignature = signature,
                RedPacketId = id
            });
        }
    }
}