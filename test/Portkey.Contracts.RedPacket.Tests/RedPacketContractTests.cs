using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.RedPacket
{
    public class RedPacketContractTests : RedPacketContractTestBase
    {
        [Fact]
        public async Task InitializeTest()
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });
            var admin = await RedPacketContractStub.GetRedPacketMaxCount.CallAsync(new Empty());
            Assert.Equal(1000, admin.MaxCount);

            var maxCount = await RedPacketContractStub.GetRedPacketMaxCount.CallAsync(new Empty());
            Assert.Equal(1000, maxCount.MaxCount);

            var result = await RedPacketContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });
            result.TransactionResult.Error.ShouldContain("Already initialized.");
        }

        [Fact]
        public async Task Initialize_WithInvalidateParam_Test()
        {
            var result = await RedPacketContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 0
            });
            result.TransactionResult.Error.ShouldContain("MaxCount should be greater than 0.");
        }


        [Fact]
        public async Task CreateRedPacketTest()
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 10
            });

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = DAppContractAddress,
                Symbol = "ELF",
                Amount = 1000
            });

            var id = Guid.NewGuid().ToString().Replace("-", "");
            var message = $"{id}-ELF-{10}-{10}";
            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray)
                    .ToHex();
            
            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var txResult = await RedPacketContractStub.CreateRedPacket.SendAsync(new CreateRedPacketInput
            {
                RedPacketSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
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
                    ReceiverAddress = receiveAddress,
                    RedPacketSignature = receiveSignature
                }
            };
            var batchInput = new TransferRedPacketBatchInput
            {
                RedPacketId = id,
                TransferRedPacketInputs = { list }
            };
            var batchResult = await RedPacketContractStub.TransferRedPacket.SendAsync(batchInput);
        }



        [Fact]
        public async Task CreateRedPacket_Invalidate_Input_Test()
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = DAppContractAddress,
                Symbol = "ELF",
                Amount = 1000
            });

            var id = Guid.NewGuid().ToString().Replace("-", "");
            var message = $"{id}-ELF-{10}-{10}";
            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray)
                    .ToHex();
            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var invalidateTotalCountResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 1000,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            invalidateTotalCountResult.TransactionResult.Error.ShouldContain(
                "TotalAmount should be greater than MinAmount * TotalCount.");

            var redPacketIdResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = ""
                });
            redPacketIdResult.TransactionResult.Error.ShouldContain("RedPacketId should not be null.");
            
            
            var redPacketSymbolResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            redPacketSymbolResult.TransactionResult.Error.ShouldContain("Symbol should not be null.");
            
            
            var redPacketTotalAmountResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 0,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            redPacketTotalAmountResult.TransactionResult.Error.ShouldContain("TotalAmount should be greater than 0.");
            
            var totalCountResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 0,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            totalCountResult.TransactionResult.Error.ShouldContain("TotalCount should be greater than 0.");
            
            var totalCountErrorResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 10000,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            totalCountErrorResult.TransactionResult.Error.ShouldContain("TotalCount should be less than or equal to MaxCount.");
            
            var expireTimeResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds - 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            expireTimeResult.TransactionResult.Error.ShouldContain("ExpiredTime should be greater than now.");
            
            
            var pubkeyResult = await RedPacketContractStub.CreateRedPacket.SendWithExceptionAsync(
                new CreateRedPacketInput
                {
                    RedPacketSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = "",
                    RedPacketType = RedPacketType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    RedPacketSignature = signature,
                    RedPacketId = id
                });
            pubkeyResult.TransactionResult.Error.ShouldContain("PublicKey should not be null.");
        }
        
        
        [Fact]
        public async Task SetGetRedPacketMaxCount_Test()
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });
            var result = await RedPacketContractStub.GetRedPacketMaxCount.CallAsync(new Empty());
            result.MaxCount.ShouldBe(1000);
            var maxCount = await RedPacketContractStub.SetRedPacketMaxCount.SendAsync(new SetRedPacketMaxCountInput
            {
                MaxCount = 500
            });
            var maxCountOutput = await RedPacketContractStub.GetRedPacketMaxCount.CallAsync(new Empty());
            maxCountOutput.MaxCount.ShouldBe(500);
        }
        
        
        [Fact]
        public async Task GetRedPacketInfo_Test()
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 10
            });

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = DAppContractAddress,
                Symbol = "ELF",
                Amount = 1000
            });

            var id = Guid.NewGuid().ToString().Replace("-", "");
            var message = $"{id}-ELF-{10}-{10}";
            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray)
                    .ToHex();
            
            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var txResult = await RedPacketContractStub.CreateRedPacket.SendAsync(new CreateRedPacketInput
            {
                RedPacketSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = publicKey,
                RedPacketType = RedPacketType.QuickTransfer,
                ExpirationTime = timeSeconds + 1000,
                RedPacketSignature = signature,
                RedPacketId = id
            });
            txResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var redPacketInfo = await RedPacketContractStub.GetRedPacketInfo.CallAsync(new GetRedPacketInput
            {
                RedPacketId = id
            });
            redPacketInfo.RedPacketInfo.RedPacketId.ShouldBe(id);
            redPacketInfo.RedPacketInfo.SenderAddress.ShouldBe(DefaultAddress);
            redPacketInfo.RedPacketInfo.PublicKey.ShouldBe(publicKey);
            
        }

        
        
    }
}