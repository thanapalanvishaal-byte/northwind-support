import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';

export function LoginPage() {
  const { login } = useAuth(); const navigate = useNavigate(); const location = useLocation();
  const [email, setEmail] = useState(''); const [password, setPassword] = useState(''); const [error, setError] = useState<string>(); const [pending, setPending] = useState(false);
  const submit = async (event: React.FormEvent) => { event.preventDefault(); if (!email.trim() || !password) { setError('Email and password are required.'); return; } setPending(true); setError(undefined); try { await login(email, password); const target = new URLSearchParams(location.search).get('returnTo') || '/tickets'; navigate(target, { replace: true }); } catch (caught) { setError(toErrorMessage(caught, 'Sign-in failed.')); } finally { setPending(false); } };
  return <section className="card form"><h1>Sign in</h1><form onSubmit={(event) => void submit(event)} noValidate>
    <label className="field__label" htmlFor="login-email">Email</label><input id="login-email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="username" />
    <label className="field__label" htmlFor="login-password">Password</label><input id="login-password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" />
    {error && <p className="field__error" role="alert">{error}</p>}<button className="button button--primary" disabled={pending}>{pending ? 'Signing in...' : 'Sign in'}</button>
  </form></section>;
}
