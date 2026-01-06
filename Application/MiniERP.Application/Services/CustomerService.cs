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
        public Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            return _customerRepository.GetAllAsync();
        }

        public async Task CreateCustomerAsync(Customer customer)
        {
             await _customerRepository.AddAsync(customer);
        }

        public Task DeleteCustomerAsync(int id)
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

        public async Task UpdateCustomerAsync(Customer customer)
        {
            await _customerRepository.UpdateAsync(customer);
        }
    }
}

