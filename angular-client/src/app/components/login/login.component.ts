import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  username = '';
  password = '';
  loading = false;
  error = '';

  constructor(private auth: AuthService) {}

  login(): void {
    if (!this.username || !this.password) return;

    this.loading = true;
    this.error = '';

    this.auth.login(this.username, this.password).subscribe({
      next: () => {
        this.loading = false;
        // AppComponent сам отреагирует на изменение isLoggedIn
        window.location.reload();
      },
      error: (err) => {
        this.error = 'Неверный логин или пароль';
        this.loading = false;
      }
    });
  }
}
