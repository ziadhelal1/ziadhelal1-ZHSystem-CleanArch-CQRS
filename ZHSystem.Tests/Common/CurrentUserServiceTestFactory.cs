using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZHSystem.Application.Common.Interfaces;

namespace ZHSystem.Test.Common
{
    public static class CurrentUserServiceTestFactory
    {
        public static Mock<ICurrentUserService> MockCurrentUser(string? userId)
        {
            var mock = new Mock<ICurrentUserService>();
            mock.Setup(x => x.UserId).Returns(userId);
            return mock;
        }
    }
}
