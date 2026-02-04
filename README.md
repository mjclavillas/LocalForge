# LocalForge
LocalForge is a lightweight **local development manager built with C#** for Windows. It allows you to run multiple local websites using `.test` domains (e.g., `project.test`, `project2.test`) by automatically configuring **Apache virtual hosts**, managing the **Windows hosts file**, and controlling local services.

LocalForge is designed to be simple, fast, and transparent—no Docker, no virtual machines, just a clean local stack you control.

---

## Features

- 🚀 Run multiple local `.test` domains effortlessly
- ⚙️ Automatic Apache virtual host generation
- 🧭 Automatic Windows hosts file management
- 🗂 Simple project-per-domain structure
- 🪶 Lightweight and fast (no containers)
- 💻 Built with C# for stability and extensibility
- 🛠 Ideal for PHP, Laravel, and static projects

---

## Folder Structure

After building LocalForge, your directory should look like this:

```
C:/LocalForge/
├── bin/
│   ├── apache/
│   ├── php/
│   └── mysql/
├── config/
│   └── apache/
│       └── httpd.conf.template
├── data/
├── etc/
│   └── apps/
│       └── phpMyAdmin/
├── logs/
├── www/
└── LocalForge.exe
```

> Files inside `dir_files` **must be placed beside `LocalForge.exe`**
> Recommended location: `C:/LocalForge`

---

## Requirements

- Windows OS
- .NET Runtime (required for C# executable)
- Administrator privileges (hosts file modification)
- Ports `80` (Apache) and `3306` (MySQL) available

---

## Installation & Setup

### 1. Build LocalForge

Build the project normally in Visual Studio or via `dotnet build`.
After building, copy the output executable (`LocalForge.exe`) to:

```
C:/LocalForge
```

---

### 2. Download Required Binaries

LocalForge does **not bundle server binaries**.

Download and extract the files from `dir_files` into `C:/LocalForge`:

#### Apache
- Apache Lounge
- https://www.apachelounge.com/download/

Extract to:
```
C:/LocalForge/bin/apache/{VERSION}
```

Example:
```
C:/LocalForge/bin/apache/httpd-2.4.66-260107-Win64-VS18
```
#### PHP
- Official PHP for Windows
- https://windows.php.net/download/

Extract to:
```
C:/LocalForge/bin/php/{VERSION}
```

Example:
```
C:/LocalForge/bin/mysql/php-8.2.30-Win32-vs16-x64
```
#### MySQL
- MySQL Community Server (ZIP Archive)
- https://dev.mysql.com/downloads/mysql/

Extract to:
```
C:/LocalForge/bin/mysql/{VERSION}
```

Example:
```
C:/LocalForge/bin/mysql/mysql-8.0.36-winx64
```
> ⚠️ Use the **ZIP archive**, not the installer.

#### Mailpit
- Mailpit (ZIP Archive)
- https://github.com/axllent/mailpit/releases/download/v1.29.0/mailpit-windows-amd64.zip

Extract to:
```
C:/LocalForge/bin/mailpit/{VERSION}
```

Example:
```
C:/LocalForge/bin/mysql/1.28.2
```
---

## Usage

### 3. Add Your Projects

Place your local sites inside:

```
C:/LocalForge/www/
```

Example:
```
www/
├── project
├── project2
├── project3
├── project4
├── project5
```

Each folder name becomes a local domain.

---

### 4. Run LocalForge

Run **LocalForge.exe as Administrator**.

LocalForge will automatically:
- Generate Apache virtual hosts
- Update the Windows hosts file
- Start and manage Apache & MySQL

Access your projects at:

```
http://project.test
http://project2.test
```

---

## phpMyAdmin

phpMyAdmin is included with LocalForge.

### Access:
```
http://localhost/phpmyadmin
```

Location:
```
C:/LocalForge/etc/apps/phpMyAdmin
```

---

## Troubleshooting

- Check `/logs` if services fail to start
- Ensure ports `80` and `3306` are not in use
- Confirm binaries are extracted correctly (no nested folders)
- Always run LocalForge as Administrator

---

## Contributing

Contributions are welcome.

1. Fork the repository
2. Create a feature or fix branch
3. Follow existing C# project structure
4. Keep commits clean and descriptive
5. Submit a pull request with clear details

Bug reports, enhancements, and refactors are all appreciated.

---

## 📄 License

This project is open-sourced software licensed under the **MIT License**.
