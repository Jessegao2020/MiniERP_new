using MiniERP.ApplicationLayer.Interfaces;
using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public Task<Customer> CreateCustomerAsync(Customer customer)
        {
            throw new NotImplementedException();
        }

        public Task DeleteCustomerAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Customer?> GetCustomerByCodeAsync(string code)
        {
            throw new NotImplementedException();
        }

        public Task<Customer?> GetCustomerByIdAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Customer>> SearchCustomersAsync(string keyword)
        {
            throw new NotImplementedException();
        }

        public Task UpdateCustomerAsync(Customer customer)
        {
            throw new NotImplementedException();
        }
    }
}

