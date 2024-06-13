## Использование PostgreSQL + pgvector

Создаем postgres контейнер:

```bash
docker-compose -f docker-compose.yml build
docker-compose -f docker-compose.yml up -d
```

Создаем БД:

```sql
CREATE DATABASE IF NOT EXISTS sharp_ner;
--- И добавляем расширение:
CREATE EXTENSION IF NOT EXISTS vector;
```

Далее приложение само создаст нужные таблицы при первом запуске.

Проблемы:
При перезагрузки проекта, если попытаться получить документ по documentId, почему-то не находит его
