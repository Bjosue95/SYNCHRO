<template>
  <div class="auth-shell">
    <header class="auth-header">
      <div class="auth-header-inner">
        <div class="auth-brand" aria-label="SYNCHRO">
          <img
            src="/assets/isotipo-fondo-oscuro.png"
            alt=""
            class="auth-symbol"
          />
          <img
            src="/assets/nombre-blanco.png"
            alt="SYNCHRO"
            class="auth-wordmark"
          />
        </div>

        <span class="auth-product">
          Administración
        </span>
      </div>
    </header>

    <main class="auth-main">
      <slot />
    </main>

    <footer class="auth-footer">
      <span>SYNCHRO</span>
      <span aria-hidden="true">·</span>
      <span>El punto donde todo se conecta</span>
    </footer>
  </div>
</template>

<style scoped>
.auth-shell {
  --auth-navy: #01305c;
  --auth-accent: #00b9c7;

  display: flex;
  flex-direction: column;
  min-height: 100vh;
  min-height: 100dvh;

  background: #f4f7f9;
  color: #01305c;
}

.auth-header {
  flex-shrink: 0;
  min-height: 62px;
  background: var(--auth-navy);
}

.auth-header-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;

  max-width: 1180px;
  min-height: 62px;
  margin: 0 auto;
  padding: 8px 24px;
}

.auth-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.auth-symbol {
  width: 36px;
  height: 36px;
  object-fit: contain;
}

.auth-wordmark {
  display: block;
  width: 148px;
  height: 37px;
  object-fit: contain;
  object-position: left center;
}

.auth-product {
  color: #d3e5ed;
  font-size: 13px;
  font-weight: 500;
}

.auth-main {
  display: flex;
  justify-content: center;
  flex: 1;

  width: 100%;
  padding: clamp(12px, 2.5vh, 24px) 16px;
}

.auth-footer {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 5px;

  flex-shrink: 0;
  padding: 9px 16px 14px;

  color: #637987;
  font-size: 12px;
  text-align: center;
}

@media (max-width: 600px) {
  .auth-header,
  .auth-header-inner {
    min-height: 56px;
  }

  .auth-header-inner {
    padding: 7px 16px;
  }

  .auth-symbol {
    width: 32px;
    height: 32px;
  }

  .auth-wordmark {
    width: 126px;
    height: 34px;
  }

  .auth-product {
    font-size: 12px;
  }

  .auth-main {
    padding: 14px 12px;
  }
}

@media (max-width: 360px) {
  .auth-product {
    display: none;
  }
}

@media (max-height: 570px) {
  .auth-main {
    padding-top: 10px;
    padding-bottom: 10px;
  }

  .auth-footer {
    padding: 5px 12px;
  }
}
</style>