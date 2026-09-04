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
            => _customerRepository.GetAllAsync();

        public Task<Customer?> GetCustomerByIdAsync(int id)
            => _customerRepository.GetByIdAsync(id);

        public Task<Customer?> GetCustomerByCodeAsync(string code)
            => _customerRepository.GetByCodeAsync(code);

        public Task<IEnumerable<Customer>> SearchCustomersAsync(string keyword)
            => _customerRepository.SearchAsync(keyword);

        public async Task CreateCustomerAsync(Customer customer)
        {
            ArgumentNullException.ThrowIfNull(customer);

            if (string.IsNullOrWhiteSpace(customer.Name))
                throw new ArgumentException("Customer name is required.", nameof(customer));

            customer.Name = customer.Name.Trim();
            await _customerRepository.AddAsync(customer);
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            ArgumentNullException.ThrowIfNull(customer);

            if (customer.Id <= 0)
                throw new ArgumentException("A persisted customer id is required.", nameof(customer));

            if (string.IsNullOrWhiteSpace(customer.Name))
                throw new ArgumentException("Customer name is required.", nameof(customer));

            customer.Name = customer.Name.Trim();
            await _customerRepository.UpdateAsync(customer);
        }

        public Task DeleteCustomerAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            return _customerRepository.DeleteAsync(id);
        }
    }
}
