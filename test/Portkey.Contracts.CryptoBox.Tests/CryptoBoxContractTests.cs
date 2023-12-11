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
using Portkey.Contracts.CryptoBox;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CryptoBox
{
    public class CryptoBoxContractTests : CryptoBoxContractTestBase
    {
        [Fact]
        public async Task InitializeTest()
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });
            var admin = await CryptoBoxContractStub.GetCryptoBoxMaxCount.CallAsync(new Empty());
            Assert.Equal(1000, admin.MaxCount);

            var maxCount = await CryptoBoxContractStub.GetCryptoBoxMaxCount.CallAsync(new Empty());
            Assert.Equal(1000, maxCount.MaxCount);

            var result = await CryptoBoxContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });
            result.TransactionResult.Error.ShouldContain("Already initialized.");
        }

        [Fact]
        public async Task Initialize_WithInvalidateParam_Test()
        {
            var result = await CryptoBoxContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
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
        public async Task CreateCryptoBoxTest()
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
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
            var txResult = await CryptoBoxContractStub.CreateCryptoBox.SendAsync(new CreateCryptoBoxInput
            {
                CryptoBoxSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = publicKey,
                CryptoBoxType = CryptoBoxType.QuickTransfer,
                ExpirationTime = timeSeconds + 1000,
                CryptoBoxSignature = signature,
                CryptoBoxId = id
            });

            var receiveAddress = DefaultAddress;
            var signatureStr =
                $"{id}-{DefaultAddress}-{10}";

            var byteArray = HashHelper.ComputeFrom(signatureStr).ToByteArray();
            var receiveSignature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), byteArray)
                    .ToHex();

            var list = new List<TransferCryptoBoxInput>
            {
                new TransferCryptoBoxInput
                {
                    Amount = 10,
                    ReceiverAddress = receiveAddress,
                    CryptoBoxSignature = receiveSignature
                }
            };
            var batchInput = new TransferCryptoBoxBatchInput
            {
                CryptoBoxId = id,
                TransferCryptoBoxInputs = { list }
            };
            var batchResult = await CryptoBoxContractStub.TransferCryptoBox.SendAsync(batchInput);
            batchResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }


        [Fact]
        public async Task CreateCryptoBox_Invalidate_Input_Test()
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
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
            var invalidateTotalCountResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 1000,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            invalidateTotalCountResult.TransactionResult.Error.ShouldContain(
                "TotalAmount should be greater than MinAmount * TotalCount.");

            var CryptoBoxIdResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = ""
                });
            CryptoBoxIdResult.TransactionResult.Error.ShouldContain("CryptoBoxId should not be null.");


            var CryptoBoxSymbolResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            CryptoBoxSymbolResult.TransactionResult.Error.ShouldContain("Symbol should not be null.");


            var CryptoBoxTotalAmountResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 0,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            CryptoBoxTotalAmountResult.TransactionResult.Error.ShouldContain("TotalAmount should be greater than 0.");

            var totalCountResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 0,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            totalCountResult.TransactionResult.Error.ShouldContain("TotalCount should be greater than 0.");

            var totalCountErrorResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 10000,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            totalCountErrorResult.TransactionResult.Error.ShouldContain(
                "TotalCount should be less than or equal to MaxCount.");

            var expireTimeResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = publicKey,
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds - 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            expireTimeResult.TransactionResult.Error.ShouldContain("ExpiredTime should be greater than now.");


            var pubkeyResult = await CryptoBoxContractStub.CreateCryptoBox.SendWithExceptionAsync(
                new CreateCryptoBoxInput
                {
                    CryptoBoxSymbol = "ELF",
                    TotalAmount = 1000,
                    TotalCount = 100,
                    MinAmount = 10,
                    SenderAddress = DefaultAddress,
                    PublicKey = "",
                    CryptoBoxType = CryptoBoxType.QuickTransfer,
                    ExpirationTime = timeSeconds + 1000,
                    CryptoBoxSignature = signature,
                    CryptoBoxId = id
                });
            pubkeyResult.TransactionResult.Error.ShouldContain("PublicKey should not be null.");
        }


        [Fact]
        public async Task SetGetCryptoBoxMaxCount_Test()
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
            {
                ContractAdmin = DefaultAddress,
                MaxCount = 1000
            });
            var result = await CryptoBoxContractStub.GetCryptoBoxMaxCount.CallAsync(new Empty());
            result.MaxCount.ShouldBe(1000);
            var maxCount = await CryptoBoxContractStub.SetCryptoBoxMaxCount.SendAsync(new SetCryptoBoxMaxCountInput
            {
                MaxCount = 500
            });
            var maxCountOutput = await CryptoBoxContractStub.GetCryptoBoxMaxCount.CallAsync(new Empty());
            maxCountOutput.MaxCount.ShouldBe(500);
        }


        [Fact]
        public async Task GetCryptoBoxInfo_Test()
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
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
            var txResult = await CryptoBoxContractStub.CreateCryptoBox.SendAsync(new CreateCryptoBoxInput
            {
                CryptoBoxSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = publicKey,
                CryptoBoxType = CryptoBoxType.QuickTransfer,
                ExpirationTime = timeSeconds + 1000,
                CryptoBoxSignature = signature,
                CryptoBoxId = id
            });
            txResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var CryptoBoxInfo = await CryptoBoxContractStub.GetCryptoBoxInfo.CallAsync(new GetCryptoBoxInput
            {
                CryptoBoxId = id
            });
            CryptoBoxInfo.CryptoBoxInfo.CryptoBoxId.ShouldBe(id);
            CryptoBoxInfo.CryptoBoxInfo.SenderAddress.ShouldBe(DefaultAddress);
            CryptoBoxInfo.CryptoBoxInfo.PublicKey.ShouldBe(publicKey);
        }

        [Fact]
        public async Task Refund_CryptoBox_Invalidate_Param_Test()
        {
            var refundInputWithoutId = new RefundCryptoBoxInput
            {
                CryptoBoxId = "",
                Amount = 100,
                CryptoBoxSignature = ""
            };
            var refundResultWithoutId =
                await CryptoBoxContractStub.RefundCryptoBox.SendWithExceptionAsync(refundInputWithoutId);
            refundResultWithoutId.TransactionResult.Error.ShouldContain("CryptoBoxId should not be null.");

            var refundInputWithErrorId = new RefundCryptoBoxInput
            {
                CryptoBoxId = "test",
                Amount = 100,
                CryptoBoxSignature = ""
            };
            var refundResultWithErrorId =
                await CryptoBoxContractStub.RefundCryptoBox.SendWithExceptionAsync(refundInputWithErrorId);
            refundResultWithErrorId.TransactionResult.Error.ShouldContain("CryptoBox not exists.");

            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();

            var CryptoBox = await CreateCryptoBox(publicKey, privateKey);

            var refundInputWithErrorExpireTime = new RefundCryptoBoxInput
            {
                CryptoBoxId = CryptoBox.CryptoBoxId,
                Amount = 100,
                CryptoBoxSignature = ""
            };
            var refundInputWithErrorExpireTimeResult =
                await CryptoBoxContractStub.RefundCryptoBox.SendWithExceptionAsync(refundInputWithErrorExpireTime);
            refundInputWithErrorExpireTimeResult.TransactionResult.Error.ShouldContain("CryptoBox not expired.");
        }

        [Fact]
        public async Task Refund_CryptoBox_Test()
        {
            var ecKeyPair = CryptoHelper.GenerateKeyPair();
            var publicKey = ecKeyPair.PublicKey.ToHex();
            var privateKey = ecKeyPair.PrivateKey.ToHex();
            var CryptoBox = await CreateCryptoBoxExpired(publicKey, privateKey);
            Thread.Sleep(2);
            var message =
                $"{CryptoBox.CryptoBoxId}-{CryptoBox.TotalAmount}";
            var message1 =
                $"{CryptoBox.CryptoBoxId}-{10000}";
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(200));

            var hashByteArray = HashHelper.ComputeFrom(message).ToByteArray();
            var signature =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray)
                    .ToHex();

            var hashByteArray1 = HashHelper.ComputeFrom(message1).ToByteArray();
            var signature1 =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray1)
                    .ToHex();

            var refundInputInvalidateSignature = new RefundCryptoBoxInput
            {
                CryptoBoxId = CryptoBox.CryptoBoxId,
                Amount = 100,
                CryptoBoxSignature = signature1
            };
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(20000));
            Thread.Sleep(2000);
            var refundSuccessResult =
                await CryptoBoxContractStub.RefundCryptoBox.SendWithExceptionAsync(refundInputInvalidateSignature);
            refundSuccessResult.TransactionResult.Error.ShouldContain("Invalid signature.");


            var message3 =
                $"{CryptoBox.CryptoBoxId}-{100}";
            var hashByteArray3 = HashHelper.ComputeFrom(message3).ToByteArray();
            var signature3 =
                CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(privateKey), hashByteArray3)
                    .ToHex();
            var refundInput = new RefundCryptoBoxInput
            {
                CryptoBoxId = CryptoBox.CryptoBoxId,
                Amount = 100,
                CryptoBoxSignature = signature3
            };
            blockTimeProvider.SetBlockTime(DateTime.UtcNow.ToTimestamp().AddMilliseconds(20000));
            var resultSuccess =
                await CryptoBoxContractStub.RefundCryptoBox.SendAsync(refundInput);
            resultSuccess.TransactionResult.Error.ShouldBe("");
        }


        private async Task<CryptoBoxInfo> CreateCryptoBox(string pubkey, string privateKey)
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
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
            var txResult = await CryptoBoxContractStub.CreateCryptoBox.SendAsync(new CreateCryptoBoxInput
            {
                CryptoBoxSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = pubkey,
                CryptoBoxType = CryptoBoxType.QuickTransfer,
                ExpirationTime = timeSeconds + 1000,
                CryptoBoxSignature = signature,
                CryptoBoxId = id
            });
            return new CryptoBoxInfo
            {
                CryptoBoxId = id,
                SenderAddress = DefaultAddress,
                TotalAmount = 1000
            };
        }


        private async Task<CryptoBoxInfo> CreateCryptoBoxExpired(string pubkey, string privateKey)
        {
            await CryptoBoxContractStub.Initialize.SendAsync(new InitializeInput
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
            var txResult = await CryptoBoxContractStub.CreateCryptoBox.SendAsync(new CreateCryptoBoxInput
            {
                CryptoBoxSymbol = "ELF",
                TotalAmount = 1000,
                TotalCount = 10,
                MinAmount = 10,
                SenderAddress = DefaultAddress,
                PublicKey = pubkey,
                CryptoBoxType = CryptoBoxType.QuickTransfer,
                ExpirationTime = timeSeconds,
                CryptoBoxSignature = signature,
                CryptoBoxId = id
            });
            return new CryptoBoxInfo
            {
                CryptoBoxId = id,
                SenderAddress = DefaultAddress,
                TotalAmount = 1000
            };
        }
    }
}