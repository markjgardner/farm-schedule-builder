interface LoginPageProps {
  login: (provider: string) => void;
}

export function LoginPage({ login }: LoginPageProps) {
  return (
    <div className="login-page">
      <h1>🐴 Farm Schedule Builder</h1>
      <p className="login-subtitle">Sign in to manage your barn schedule</p>
      <div className="login-buttons">
        <button className="login-btn microsoft" onClick={() => login('aad')}>
          Sign in with Microsoft
        </button>
        <button className="login-btn google" onClick={() => login('google')}>
          Sign in with Google
        </button>
        <button
          className="login-btn facebook"
          onClick={() => login('facebook')}
        >
          Sign in with Facebook
        </button>
      </div>
    </div>
  );
}
