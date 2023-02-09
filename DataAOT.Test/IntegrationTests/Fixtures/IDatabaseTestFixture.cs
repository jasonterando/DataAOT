using DataAOT.Test.TestClasses;

namespace DataAOT.Test.IntegrationTests.Fixtures;

public interface IDatabaseTestFixture: IDisposable
{
    IDbGateway<UserAccountModel> CreateUserAccountGateway();
}
