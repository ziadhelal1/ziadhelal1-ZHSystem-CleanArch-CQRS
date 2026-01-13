using AutoMapper;
using Microsoft.Extensions.Logging;
using ZHSystem.Application.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZHSystem.Application.UnitTests.Common
{
    public class AutoMapperTestFactory
    {
        public static IMapper CreateMapper()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();

            var loggerFactory = LoggerFactory.Create(builder => { });
                
            var config = new MapperConfiguration(configExpression, loggerFactory);

            config.AssertConfigurationIsValid();

            return config.CreateMapper();
        }
    }
}
