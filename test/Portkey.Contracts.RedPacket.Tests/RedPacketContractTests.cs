using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
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

           await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = DAppContractAddress,
                Symbol = "ELF",
                Amount = 1000
            });

            var message = $"{"ELF"}-{10}-{10}";
            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray)
                    .ToHex();
            var id = Guid.NewGuid().ToString().Replace("-","");
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

            var receiveAddress = DefaultAddress;
            var signatureStr =
                $"{id}-{DefaultAddress}-{10}";
            
            var byteArray = HashHelper.ComputeFrom(signatureStr).ToByteArray();
            var receiveSignature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), byteArray)
                    .ToHex();
            
            var list = new List<TransferRedPacketInput>
            {
                  new TransferRedPacketInput
                    {
                        Amount = 10,
                        RedPacketId = id,
                        ReceiverAddress = receiveAddress,
                        RedPacketSignature = receiveSignature
                    }   
            };
            var batchInput = new TransferRedPacketBatchInput
            {
                RedPacketId = id,
                TransferRedPacketInputs = {list}
            };
            var batchResult = await RedPacketContractStub.TransferRedPacket.SendAsync(batchInput);
        }
    }
}