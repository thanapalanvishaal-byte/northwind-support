import { Link, NavLink, Outlet } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthContext';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  return <AuthProvider><AppShell /></AuthProvider>;
}

function AppShell() {
  const { user, logout } = useAuth();
  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
        {user && <div>{user.fullName} <button type="button" className="button button--ghost" onClick={logout}>Log out</button></div>}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
