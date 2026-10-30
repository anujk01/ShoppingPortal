# ShoppingPortal

ASP.NET Core API for the separate React/Vite storefront in `../ShoppingPortal.Client/`.

## Run locally

1. Run `npm install` once in `../ShoppingPortal.Client`.
2. Open `ShoppingPortal.sln` in Rider and run the `http` profile. In Development, the backend starts Vite from the sibling React project and redirects `http://localhost:5187/` to `http://localhost:5173/`.
3. Request an OTP in the sign-in page. In the default Development mode the code is shown in the UI and written to the API log.
4. Open API docs at `http://localhost:5187/swagger`.

If the API runs at a different address, copy `../ShoppingPortal.Client/.env.example` to `../ShoppingPortal.Client/.env.local`, update `VITE_API_URL`, and restart Vite.

## API surface

- `POST /api/auth/request-otp` — `{ "value": "you@example.com", "kind": "Email" }` (or `"Mobile"`).
- `POST /api/auth/verify-otp` — `{ "value": "you@example.com", "kind": "Email", "code": "123456" }`; returns a bearer token.
- `GET /api/auth/me` — current contact; requires `Authorization: Bearer <token>`.
- `GET /api/catalog/categories` — starter categories.
- `GET /api/catalog/products?categoryId=electronics` — starter products; omit the query to list all.

## Architecture

`Controllers/` handles HTTP; `Application/` contains DTOs and service contracts/use cases; `Infrastructure/` contains the demo OTP/session stores, configurable Resend/Twilio Messaging delivery adapters, and starter catalog.

## Configure real OTP delivery

The default Development configuration logs codes locally. Provider credentials are loaded from .NET User Secrets and are not stored in source files. From the project directory, set up Resend email:

```sh
dotnet user-secrets set "OtpDelivery:EmailProvider" "Resend"
dotnet user-secrets set "OtpDelivery:Resend:ApiKey" "re_your_api_key"
dotnet user-secrets set "OtpDelivery:Resend:FromEmail" "Foundry <otp@your-verified-domain.example>"
```

For mobile OTP, use Twilio Programmable Messaging. Configure the Account SID and Auth Token from your Twilio account, plus the sender number shown in your curl (`From`). The API sends the generated OTP in the message body:

```sh
dotnet user-secrets set "OtpDelivery:SmsProvider" "Twilio"
dotnet user-secrets set "OtpDelivery:Twilio:AccountSid" "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
dotnet user-secrets set "OtpDelivery:Twilio:AuthToken" "your_auth_token"
dotnet user-secrets set "OtpDelivery:Twilio:FromNumber" "+1xxxxxxxxxx"
```

Restart the API after changing secrets. Resend requires a valid sender; use a verified sending domain for delivery to arbitrary recipients. Twilio trial access is limited: recipients must be verified, and trial usage/expiration and message restrictions apply. To continue using local codes, leave the provider set to `Development`.

## Before production

OTP codes and sessions are in-memory and are lost on restart; the Development delivery adapter only logs codes in Development. Configure a persistent data store, distributed rate limiting, token revocation/refresh, stronger anti-abuse controls, and production secrets before deployment. The client currently uses starter catalog content and local-only UI state for cart/favorites.
