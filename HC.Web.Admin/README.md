# HCWebAdmin

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 19.2.15.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4222/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Authentication

The admin area is protected by the JWT that the API (`https://serv.homecuties.com/api/admin`) issues
on a successful login:

* `POST /api/admin/login` returns `token` (a signed HS256 JWT) and `expiresOn`. The token carries the
  user id, the login id, the roles, the login time (`iat`) and the expiry (`exp`).
* `AdminTokenInterceptor` sends every admin request with `Authorization: Bearer <token>`. The API
  validates the token (signature, issuer, audience, expiry, account still active) before an action
  runs, so a request without a valid token is answered with `401`.
* `AuthGuard` keeps signed-out sessions out of the shell and `MenuAccessGuard` only opens the sections
  the roles grant. Both read the sections from `PermissionsService`, which loads them from
  `POST /api/admin/menus` - the API derives that list from the JWT roles and the `AdminMenusRoles`
  mapping, the same rule the API enforces.
* A session lasts `Jwt:ExpiryMinutes` (default 60 minutes); when the API answers `401` the token and
  the cached user are dropped and the admin is sent back to `/login`.

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
