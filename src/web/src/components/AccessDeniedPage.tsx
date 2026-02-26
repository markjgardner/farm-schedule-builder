interface AccessDeniedPageProps {
  userDetails: string;
  logout: () => void;
}

export function AccessDeniedPage({ userDetails, logout }: AccessDeniedPageProps) {
  return (
    <div className="login-page">
      <h1>🐴 Farm Schedule Builder</h1>
      <h2>Access Denied</h2>
      <p>
        You are signed in as <strong>{userDetails}</strong>, but your account
        has not been registered by an administrator.
      </p>
      <p>Please contact a farm administrator to be added to the system.</p>
      <button className="login-btn microsoft" onClick={logout}>
        Sign Out
      </button>
    </div>
  );
}
