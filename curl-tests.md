# FlashSale API Curl Tests

## 1. POST /register

### Success Case
```bash
curl -X POST http://localhost:5000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "buyer@test.com", "password": "SecurePass123!"}'

  curl -X POST http://localhost:5000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "buyer2@test.com"}'
