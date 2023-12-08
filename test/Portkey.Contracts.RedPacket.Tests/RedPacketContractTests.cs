using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.CSharp.Core.Extension;
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
        public async Task Test()
        {
            Address ad = Address.FromBase58("26MWta7rdxAVo8eYLPYfa1U442TWc7q834yGJZUJMkjDcAfvNZ");
            var amount = 1000;
            var str = $"{ad.ToBase58()}-{amount}";
            var message =
                "3f0c022f-b90c-4883-9c91-dc0bb6c2a430-26MWta7rdxAVo8eYLPYfa1U442TWc7q834yGJZUJMkjDcAfvNZ-10000000";
            var sig =
                "d57c93ab02dcf8fad9e9928495fcc1b1988d5cab71409ed9f342d6e2c7f3b9686137c7cb4c40070c5522fb6b8f430b47e35e55d956a0a9f274b154927fabd7f300";
            var pub =
                "04a05adebea2df2c26dc1f8e009b3b3015da765a2bf44de6b6880f32619d310b0825e22fc2fdaf983ff4475fdd2439ec343a082fedd98f87008541a81a1f9377ec";
            var result = VerifySignature(pub, sig, message);
        }

        private bool VerifySignature(string publicKey, string signature, string message)
        {
            var messageBytes = HashHelper.ComputeFrom(message).ToByteArray();
            var signatureBytes = ByteStringHelper.FromHexString(signature).ToByteArray();
            CryptoHelper.RecoverPublicKey(signatureBytes, messageBytes, out var recoverPublicKey);
            var recoverKey = recoverPublicKey.ToHex();
            return recoverPublicKey.ToHex() == publicKey;
        }

        // var dataHash = HashHelper.ComputeFrom(rawData).ToByteArray();
        // var publicKeyByte = ByteArrayHelper.HexStringToByteArray(publicKey);
        // var signByte = ByteString.FromBase64(signature);
        //     return CryptoHelper.VerifySignature(signByte.ToByteArray(), dataHash, publicKeyByte);

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

            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1000;
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
            batchResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
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
            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
            totalCountErrorResult.TransactionResult.Error.ShouldContain(
                "TotalCount should be less than or equal to MaxCount.");

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

            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1000;
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

        [Fact]
        public async Task Refund_RedPacket_Invalidate_Param_Test()
        {
            var refundInputWithoutId = new RefundRedPacketInput
            {
                RedPacketId = "",
                Amount = 100,
                RedPacketSignature = ""
            };
            var refundResultWithoutId =
                await RedPacketContractStub.RefundRedPacket.SendWithExceptionAsync(refundInputWithoutId);
            refundResultWithoutId.TransactionResult.Error.ShouldContain("RedPacketId should not be null.");

            var refundInputWithErrorId = new RefundRedPacketInput
            {
                RedPacketId = "test",
                Amount = 100,
                RedPacketSignature = ""
            };
            var refundResultWithErrorId =
                await RedPacketContractStub.RefundRedPacket.SendWithExceptionAsync(refundInputWithErrorId);
            refundResultWithErrorId.TransactionResult.Error.ShouldContain("RedPacket not exists.");

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();

            var redPacket = await CreateRedPacket(publicKey, privateKey);

            var refundInputWithErrorExpireTime = new RefundRedPacketInput
            {
                RedPacketId = redPacket.RedPacketId,
                Amount = 100,
                RedPacketSignature = ""
            };
            var refundInputWithErrorExpireTimeResult =
                await RedPacketContractStub.RefundRedPacket.SendWithExceptionAsync(refundInputWithErrorExpireTime);
            refundInputWithErrorExpireTimeResult.TransactionResult.Error.ShouldContain("RedPacket not expired.");
        }

        [Fact]
        public async Task Refund_RedPacket_Test()
        {
            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();
            var redPacket = await CreateRedPacketExpired(publicKey, privateKey);
            Thread.Sleep(2);
            var message =
                $"{redPacket.RedPacketId}-{redPacket.TotalAmount}";
            var message1 =
                $"{redPacket.RedPacketId}-{10000}";
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(200));

            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray)
                    .ToHex();

            var hashByteArray1 = HashHelper.ComputeFrom(message1).ToByteArray();
            var signature1 =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray1)
                    .ToHex();

            var refundInputInvalidateSignature = new RefundRedPacketInput
            {
                RedPacketId = redPacket.RedPacketId,
                Amount = 100,
                RedPacketSignature = signature1
            };
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(20000));
            Thread.Sleep(2000);
            var refundSuccessResult =
                await RedPacketContractStub.RefundRedPacket.SendWithExceptionAsync(refundInputInvalidateSignature);
            refundSuccessResult.TransactionResult.Error.ShouldContain("Invalid signature.");


            var message3 =
                $"{redPacket.RedPacketId}-{100}";
            var hashByteArray3 = HashHelper.ComputeFrom(message3).ToByteArray();
            var signature3 =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray3)
                    .ToHex();
            var refundInput = new RefundRedPacketInput
            {
                RedPacketId = redPacket.RedPacketId,
                Amount = 100,
                RedPacketSignature = signature3
            };
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(20000));
            var resultSuccess =
                await RedPacketContractStub.RefundRedPacket.SendAsync(refundInput);
            resultSuccess.TransactionResult.Error.ShouldBe("");
        }


        private async Task<RedPacketInfo> CreateRedPacket(string pubkey, string privateKey)
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 10
            });


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

            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var txResult = await RedPacketContractStub.CreateRedPacket.SendAsync(new CreateRedPacketInput
            {
                RedPacketSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = pubkey,
                RedPacketType = RedPacketType.QuickTransfer,
                ExpirationTime = timeSeconds + 1000,
                RedPacketSignature = signature,
                RedPacketId = id
            });
            return new RedPacketInfo
            {
                RedPacketId = id,
                SenderAddress = DefaultAddress,
                TotalAmount = 1000
            };
        }


        private async Task<RedPacketInfo> CreateRedPacketExpired(string pubkey, string privateKey)
        {
            await RedPacketContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 10
            });


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

            var timeSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1;
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(-2));
            var txResult = await RedPacketContractStub.CreateRedPacket.SendAsync(new CreateRedPacketInput
            {
                RedPacketSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = pubkey,
                RedPacketType = RedPacketType.QuickTransfer,
                ExpirationTime = timeSeconds,
                RedPacketSignature = signature,
                RedPacketId = id
            });
            return new RedPacketInfo
            {
                RedPacketId = id,
                SenderAddress = DefaultAddress,
                TotalAmount = 1000
            };
        }
    }
}