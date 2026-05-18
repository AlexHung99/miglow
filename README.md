# Postoria Auth Frontend

Static GitHub Pages prototype for Postoria member login, registration, and forgot-password flows.

## Local Preview

```powershell
node server.mjs
```

Open:

```text
http://localhost:4173
```

## API Endpoint

The frontend defaults to:

```text
http://122.116.181.61:5073
```

Override it from the browser console:

```js
localStorage.setItem("postoria-api-base", "https://your-api-host.example.com");
location.reload();
```

GitHub Pages is served over HTTPS. Browsers block calls from HTTPS pages to plain HTTP APIs, so live API testing from GitHub Pages needs the Postoria API to be available through HTTPS or an HTTPS proxy.
